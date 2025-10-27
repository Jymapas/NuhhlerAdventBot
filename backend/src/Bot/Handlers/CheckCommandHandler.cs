using System;
using Application.Abstractions;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Bot.Keyboards;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class CheckCommandHandler : HandlerBase, ICommandHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CheckCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<CheckCommandHandler> logger,
        IServiceScopeFactory scopeFactory)
        : base(fsmStorage, configuration, logger)
    {
        _scopeFactory = scopeFactory;
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/check", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        if (update.Message?.From is null)
        {
            return;
        }

        if (!IsOwner(update.Message.From.Id, update.Message.From.Username))
        {
            await ReplyAsync(
                client,
                GetChatId(update),
                "У вас нет прав использовать эту команду.",
                cancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var adventRepository = scope.ServiceProvider.GetRequiredService<IAdventRepository>();

        var owner = await userRepository.EnsureAsync(update.Message.From.Id, update.Message.From.Username, update.Message.From.FirstName, cancellationToken);
        var campaign = await adventRepository.GetActiveOrDraftByOwnerAsync(owner.Id, cancellationToken);

        if (campaign is null)
        {
            await ReplyAsync(
                client,
                GetChatId(update),
                "Сначала создайте кампанию через /new_advent.",
                cancellationToken);
            return;
        }

        await client.SendMessage(
            new ChatId(GetChatId(update)),
            "Выбери диапазон дней",
            replyMarkup: CheckKeyboards.BuildRangeKeyboard(),
            cancellationToken: cancellationToken);
    }
}
