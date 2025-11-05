using System;
using System.Globalization;
using Application.Abstractions;
using Domain.Advent;
using Bot.Callbacks;
using Bot.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers.Callbacks;

public sealed class DailyBriefPauseCallbackHandler : ICallbackHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DailyBriefPauseCallbackHandler> _logger;

    public DailyBriefPauseCallbackHandler(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DailyBriefPauseCallbackHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool CanHandle(string data) => data.StartsWith("brief:pause:", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null || callbackQuery.From is null)
            return;

        if (!AuthExtensions.IsAllowedOwner(callbackQuery.From.Id, _configuration))
        {
            await client.AnswerCallbackQuery(
                callbackQuery.Id,
                "Недоступно",
                showAlert: true,
                cancellationToken: cancellationToken);
            return;
        }

        var data = callbackQuery.Data ?? string.Empty;
        var parts = data.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !long.TryParse(parts[2], out var campaignId) ||
            !DateOnly.TryParseExact(parts[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            await client.AnswerCallbackQuery(callbackQuery.Id, "Не удалось разобрать команду.", cancellationToken: cancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();

        try
        {
            var user = await userRepository.EnsureAsync(callbackQuery.From.Id, callbackQuery.From.Username, callbackQuery.From.FirstName, cancellationToken);
            var campaign = await adventRepository.GetCampaignAsync(campaignId, cancellationToken);
            if (!OwnershipGuards.IsCampaignOwner(campaign, user.Id))
            {
                await client.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Недоступно",
                    showAlert: true,
                    cancellationToken: cancellationToken);
                return;
            }

            if (campaign.Status != CampaignStatus.Paused)
            {
                campaign.Status = CampaignStatus.Paused;
                await adventRepository.UpdateCampaignAsync(campaign, cancellationToken);
            }

            await client.SendMessage(callbackQuery.Message.Chat.Id, "Кампания поставлена на паузу. Автоотправки приостановлены.", cancellationToken: cancellationToken);
            await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause campaign {CampaignId}", campaignId);
            await client.AnswerCallbackQuery(callbackQuery.Id, "Произошла ошибка.", cancellationToken: cancellationToken);
        }
    }
}
