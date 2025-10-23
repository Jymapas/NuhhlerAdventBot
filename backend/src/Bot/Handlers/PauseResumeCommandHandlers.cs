using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class PauseResumeCommandHandler : HandlerBase, ICommandHandler
{
    public PauseResumeCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<PauseResumeCommandHandler> logger)
        : base(fsmStorage, configuration, logger)
    {
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/pause", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/resume", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        var userId = GetUserId(update);
        var username = GetUsername(update);
        if (userId is not null)
        {
            EnsureOwner(userId.Value, username);
        }

        var text = string.Equals(command, "/pause", StringComparison.OrdinalIgnoreCase)
            ? "Пауза кампании будет доступна позже (этап 8)."
            : "Возобновление кампании будет реализовано на этапе 8.";

        await ReplyAsync(client, GetChatId(update), text, cancellationToken);
    }
}
