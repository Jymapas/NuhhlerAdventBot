using System;
using Application.Abstractions;
using Application.Services;
using Bot.Handlers.Common;
using Domain.Advent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers.System;

public sealed class StartWithBindHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StartWithBindHandler> _logger;

    public StartWithBindHandler(IServiceScopeFactory scopeFactory, ILogger<StartWithBindHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ITelegramBotClient client, Update update, string? args, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args) || !args.StartsWith("bind:", StringComparison.OrdinalIgnoreCase))
            return false;

        if (update.Message?.From is null)
            return true;

        var token = args["bind:".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            await client.SendMessage(new ChatId(update.Message.Chat.Id), "Ссылка недействительна.", cancellationToken: cancellationToken);
            return true;
        }

        using var scope = _scopeFactory.CreateScope();
        var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var campaignService = new CampaignService(adventRepository);

        var campaign = await adventRepository.GetByBindTokenAsync(token, cancellationToken);
        if (campaign is null)
        {
            await client.SendMessage(new ChatId(update.Message.Chat.Id), "Ссылка недействительна.", cancellationToken: cancellationToken);
            return true;
        }

        if (campaign.RecipientStatus != RecipientStatus.Pending || string.IsNullOrWhiteSpace(campaign.BindToken))
        {
            await client.SendMessage(new ChatId(update.Message.Chat.Id), "Ссылка недействительна.", cancellationToken: cancellationToken);
            return true;
        }

        var user = await userRepository.EnsureAsync(update.Message.From.Id, update.Message.From.Username, update.Message.From.FirstName, cancellationToken);

        await campaignService.AssignRecipientAsync(campaign.Id, user.Id, cancellationToken);

        await client.SendMessage(
            new ChatId(update.Message.Chat.Id),
            $"Вас назначили получателем календаря {campaign.Name}.",
            cancellationToken: cancellationToken);

        var owner = await userRepository.GetByIdAsync(campaign.OwnerUserId, cancellationToken);
        if (owner?.TelegramId is long ownerTelegramId && ownerTelegramId != 0)
        {
            var linkName = HandlerBase.GetUsernameLink(update.Message.From);
            await client.SendMessage(
                new ChatId(ownerTelegramId),
                $"Получатель {linkName} привязался; кампания готова к запуску.",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation("Recipient {RecipientId} confirmed bind for campaign {CampaignId}", user.Id, campaign.Id);
        return true;
    }
}
