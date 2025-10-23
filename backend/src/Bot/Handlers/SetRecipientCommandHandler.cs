using System;
using Application.Abstractions;
using Application.Services;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Domain.Advent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class SetRecipientCommandHandler : HandlerBase, ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SetRecipientCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<SetRecipientCommandHandler> logger,
        IServiceScopeFactory scopeFactory)
        : base(fsmStorage, configuration, logger)
    {
        _scopeFactory = scopeFactory;
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/set_recipient", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        if (update.Message?.From is null)
            return;

        if (string.IsNullOrWhiteSpace(args))
        {
            await ReplyAsync(client, GetChatId(update), "Используйте формат: /set_recipient @username", cancellationToken);
            return;
        }

        var lookup = args.Trim();
        if (lookup.StartsWith("@", StringComparison.Ordinal))
        {
            lookup = lookup[1..];
        }

        if (string.IsNullOrWhiteSpace(lookup))
        {
            await ReplyAsync(client, GetChatId(update), "Не удалось распознать имя пользователя.", cancellationToken);
            return;
        }

        var telegramUser = update.Message.From;
        EnsureOwner(telegramUser.Id, telegramUser.Username);

        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();
        var campaignService = new CampaignService(adventRepository);

        var owner = await userRepository.EnsureAsync(telegramUser.Id, telegramUser.Username, telegramUser.FirstName, cancellationToken);
        var draft = await adventRepository.GetDraftByOwnerAsync(owner.Id, cancellationToken);
        if (draft is null)
        {
            await ReplyAsync(client, GetChatId(update), "Сначала создайте кампанию командой /new_advent.", cancellationToken);
            return;
        }

        var recipient = await userRepository.GetByUsernameAsync(lookup, cancellationToken);
        if (recipient is not null)
        {
            await campaignService.AssignRecipientAsync(draft.Id, recipient.Id, cancellationToken);
            Logger.LogInformation("Recipient {RecipientId} assigned to campaign {CampaignId}", recipient.Id, draft.Id);

            await ReplyAsync(
                client,
                GetChatId(update),
                $"Получатель @{lookup} назначен. Кампания готова к запуску.",
                cancellationToken);
            return;
        }

        draft.RecipientUserId = null;
        draft.RecipientStatus = RecipientStatus.Pending;
        draft.BindToken = Guid.NewGuid().ToString("N");
        await adventRepository.UpdateCampaignAsync(draft, cancellationToken);

        var botProfile = await client.GetMe(cancellationToken);
        if (string.IsNullOrWhiteSpace(botProfile.Username))
        {
            await ReplyAsync(
                client,
                GetChatId(update),
                "Не удалось определить имя бота для формирования ссылки. Попробуйте позже.",
                cancellationToken);
            return;
        }

        var link = $"t.me/{botProfile.Username}?start=bind:{draft.BindToken}";
        Logger.LogInformation("Generated bind token for campaign {CampaignId}", draft.Id);

        var message =
            $"Пользователь @{lookup} ещё не зарегистрирован.\n" +
            $"{link}\n" +
            "Передайте ссылку получателю. Когда он её нажмёт, кампания будет привязана автоматически.";

        await ReplyAsync(
            client,
            GetChatId(update),
            message,
            cancellationToken);
    }
}
