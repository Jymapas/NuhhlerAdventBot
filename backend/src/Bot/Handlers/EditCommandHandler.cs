using System.Globalization;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Bot.Keyboards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class EditCommandHandler : HandlerBase, ICommandHandler
{
    public EditCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<EditCommandHandler> logger)
        : base(fsmStorage, configuration, logger)
    {
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/edit", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        var userId = GetUserId(update);
        if (userId is null)
        {
            Logger.LogWarning("Edit command without user id.");
            return;
        }

        var username = GetUsername(update);
        if (!IsOwner(userId.Value, username))
        {
            await ReplyAsync(
                client,
                GetChatId(update),
                "У вас нет прав использовать эту команду.",
                cancellationToken);
            return;
        }

        FsmSnapshot snapshot;
        string message;

        if (string.IsNullOrWhiteSpace(args))
        {
            snapshot = new FsmSnapshot
            {
                UserId = userId.Value,
                State = FsmState.EditAwaitDate
            };

            message = "Укажите дату в формате YYYY-MM-DD для редактирования дня.";
        }
        else if (DateOnly.TryParseExact(args, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            snapshot = new FsmSnapshot
            {
                UserId = userId.Value,
                State = FsmState.EditAwaitText,
                Payload = new Dictionary<string, string>
                {
                    ["date"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }
            };

            message = $"Введите новый текст для {date:yyyy-MM-dd}.";
        }
        else
        {
            snapshot = new FsmSnapshot
            {
                UserId = userId.Value,
                State = FsmState.EditAwaitDate
            };

            message = "Не получилось распознать дату. Попробуйте ещё раз в формате YYYY-MM-DD.";
        }

        await FsmStorage.SetAsync(snapshot);

        await ReplyAsync(
            client,
            GetChatId(update),
            $"{message} Для отмены используйте /cancel.",
            MainKeyboards.CreateCancel(),
            cancellationToken);
    }
}
