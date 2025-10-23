using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class TodayCommandHandler : HandlerBase, ICommandHandler
{
    public TodayCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<TodayCommandHandler> logger)
        : base(fsmStorage, configuration, logger)
    {
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/today", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        var userId = GetUserId(update);
        var username = GetUsername(update);
        if (userId is not null)
        {
            EnsureOwner(userId.Value, username);
        }

        await ReplyAsync(
            client,
            GetChatId(update),
            "Ежедневное превью будет на этапе 9.",
            cancellationToken);
    }
}
