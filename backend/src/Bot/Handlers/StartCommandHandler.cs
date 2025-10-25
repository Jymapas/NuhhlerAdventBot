using Application.Abstractions;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Bot.Keyboards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class StartCommandHandler : HandlerBase, ICommandHandler
{
    private readonly IBotInfo _botInfo;

    public StartCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<StartCommandHandler> logger,
        IBotInfo botInfo)
        : base(fsmStorage, configuration, logger)
    {
        _botInfo = botInfo;
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
