using System;
using Application.Abstractions;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Bot.Handlers.System;
using Bot.Keyboards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class StartCommandHandler : HandlerBase, ICommandHandler
{
    private readonly IBotInfo _botInfo;
    private readonly StartWithBindHandler _bindHandler;

    public StartCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<StartCommandHandler> logger,
        IBotInfo botInfo,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory)
        : base(fsmStorage, configuration, logger)
    {
        _botInfo = botInfo;
        _bindHandler = new StartWithBindHandler(scopeFactory, loggerFactory.CreateLogger<StartWithBindHandler>());
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/start", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/ping", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        var chatId = GetChatId(update);
        var userId = GetUserId(update);

        if (userId is not null)
        {
            await FsmStorage.ClearAsync(userId.Value);
        }

        if (string.Equals(command, "/start", StringComparison.OrdinalIgnoreCase) &&
            await _bindHandler.HandleAsync(client, update, args, cancellationToken))
        {
            return;
        }

        if (string.Equals(command, "/ping", StringComparison.OrdinalIgnoreCase))
        {
            await ReplyAsync(client, chatId, $"pong ({_botInfo.Version})", cancellationToken);
            return;
        }

        var text =
            "Привет! Этот Advent-бот помогает готовить кампанию рассылок.\n" +
            "Команды пока в режиме заглушек и будут расширяться на следующих этапах.";

        await ReplyAsync(
            client,
            chatId,
            text,
            MainKeyboards.CreateMainMenu(),
            cancellationToken);
    }
}
