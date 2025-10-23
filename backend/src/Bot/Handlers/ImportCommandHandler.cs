using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Bot.Keyboards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class ImportCommandHandler : HandlerBase, ICommandHandler
{
    public ImportCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<ImportCommandHandler> logger)
        : base(fsmStorage, configuration, logger)
    {
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/import", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        var userId = GetUserId(update);
        if (userId is null)
        {
            Logger.LogWarning("Cannot start import FSM without user id.");
            return;
        }

        var username = GetUsername(update);
        EnsureOwner(userId.Value, username);

        var snapshot = new FsmSnapshot
        {
            UserId = userId.Value,
            State = FsmState.ImportAwaitFile
        };

        await FsmStorage.SetAsync(snapshot);

        await ReplyAsync(
            client,
            GetChatId(update),
            "Пришлите файл CSV/XLS/XLSX. Для отмены используйте /cancel или кнопку «Отмена».",
            MainKeyboards.CreateCancel(),
            cancellationToken);
    }
}
