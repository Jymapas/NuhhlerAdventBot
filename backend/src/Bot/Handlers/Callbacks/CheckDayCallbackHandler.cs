using System;
using System.Globalization;
using Application.Abstractions;
using Domain.Advent;
using Bot.Callbacks;
using Bot.Security;
using Microsoft.Extensions.Configuration;
using Bot.Keyboards;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers.Callbacks;

public sealed class CheckDayCallbackHandler : ICallbackHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CheckDayCallbackHandler> _logger;

    public CheckDayCallbackHandler(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CheckDayCallbackHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool CanHandle(string data) => data.StartsWith("check:day:", StringComparison.OrdinalIgnoreCase);

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

        var dayPart = callbackQuery.Data? ["check:day:".Length..];
        if (string.IsNullOrWhiteSpace(dayPart) || !int.TryParse(dayPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day))
        {
            await client.AnswerCallbackQuery(callbackQuery.Id, "Не удалось определить день.", cancellationToken: cancellationToken);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();

            var owner = await userRepository.EnsureAsync(callbackQuery.From.Id, callbackQuery.From.Username, callbackQuery.From.FirstName, cancellationToken);
            var campaign = await adventRepository.GetActiveOrDraftByOwnerAsync(owner.Id, cancellationToken);

            if (campaign is null)
            {
                await client.AnswerCallbackQuery(callbackQuery.Id, "Нет активной кампании.", cancellationToken: cancellationToken);
                return;
            }

            var date = new DateOnly(campaign.StartDate.Year, 12, day);
            var dayEntry = await adventRepository.GetDayAsync(campaign.Id, date, cancellationToken)
                           ?? new AdventDay
                           {
                               CampaignId = campaign.Id,
                               Date = date,
                               Text = string.Empty
                           };

            var time = dayEntry.OverrideSendTime ?? campaign.DefaultSendTime;
            var timeLabel = dayEntry.OverrideSendTime.HasValue ? "(override)" : "(по умолчанию)";
            var dateIso = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var preview = $"Дата: {dateIso}\nВремя отправки: {time:HH:mm} {timeLabel}\n\nТекст:\n{dayEntry.Text ?? string.Empty}";

            await client.EditMessageText(
                callbackQuery.Message.Chat.Id,
                callbackQuery.Message.MessageId,
                preview,
                replyMarkup: CheckKeyboards.BuildDayActionsKeyboard(dateIso),
                cancellationToken: cancellationToken);

            await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle day preview for callback {Data}", callbackQuery.Data);
            await client.AnswerCallbackQuery(callbackQuery.Id, "Не удалось показать день.", cancellationToken: cancellationToken);
        }
    }
}
