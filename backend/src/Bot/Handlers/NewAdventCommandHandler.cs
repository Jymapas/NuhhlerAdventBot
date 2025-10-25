using System;
using Application.Abstractions;
using Application.Services;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class NewAdventCommandHandler : HandlerBase, ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    public NewAdventCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<NewAdventCommandHandler> logger,
        IServiceScopeFactory scopeFactory)
        : base(fsmStorage, configuration, logger)
    {
        _scopeFactory = scopeFactory;
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/new_advent", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        if (update.Message?.From is null)
            return;

        var telegramUser = update.Message.From;
        EnsureOwner(telegramUser.Id, telegramUser.Username);

        var targetYear = ResolveTargetYear(args);
        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();
        var campaignService = new CampaignService(adventRepository);

        var owner = await userRepository.EnsureAsync(telegramUser.Id, telegramUser.Username, telegramUser.FirstName, cancellationToken);

        try
        {
            var campaign = await campaignService.CreateDefaultDecemberCampaignAsync(owner.Id, targetYear, cancellationToken);
            Logger.LogInformation("Created campaign {CampaignId} for owner {OwnerId} with {DayCount} days", campaign.Id, owner.Id, campaign.Days.Count);

            var message =
                $"Кампания Advent-{targetYear} создана. Всего 31 день (1–31 декабря).\n" +
                "Назначьте получателя командой /set_recipient @username.";

            await ReplyAsync(
                client,
                GetChatId(update),
                message,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            var existing = await adventRepository.GetDraftByOwnerAsync(owner.Id, cancellationToken);
            if (existing is not null)
            {
                Logger.LogWarning("Owner {OwnerId} attempted to create new campaign while draft {CampaignId} exists", owner.Id, existing.Id);
                await ReplyAsync(
                    client,
                    GetChatId(update),
                    $"У вас уже есть незавершённая кампания (ID = {existing.Id}).",
                    cancellationToken);
            }
            else
            {
                Logger.LogError(ex, "Failed to create campaign for owner {OwnerId}", owner.Id);
                await ReplyAsync(
                    client,
                    GetChatId(update),
                    "Не удалось создать кампанию. Попробуйте позже.",
                    cancellationToken);
            }
        }
    }

    private static int ResolveTargetYear(string? args)
    {
        var currentYear = DateTime.UtcNow.Year;
        if (string.IsNullOrWhiteSpace(args))
            return currentYear;

        var trimmed = args.Trim();
        if (string.Equals(trimmed, "next", StringComparison.OrdinalIgnoreCase))
            return currentYear + 1;

        if (int.TryParse(trimmed, out var requestedYear) && requestedYear > currentYear)
            return requestedYear;

        return currentYear;
    }
}
