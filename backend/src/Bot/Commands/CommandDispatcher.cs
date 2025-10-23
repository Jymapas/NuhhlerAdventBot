using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public interface ICommandDispatcher
{
    Task DispatchAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken);
}

public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IEnumerable<ICommandHandler> _handlers;
    private readonly IFsmStorage _fsmStorage;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        IEnumerable<ICommandHandler> handlers,
        IFsmStorage fsmStorage,
        ILogger<CommandDispatcher> logger)
    {
        _handlers = handlers;
        _fsmStorage = fsmStorage;
        _logger = logger;
    }

    public async Task DispatchAsync(ITelegramBotClient client, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } text, Chat: { } chat, From: { } from })
        {
            return;
        }

        var (command, args) = HandlerBase.ParseCommand(text);
        if (string.IsNullOrEmpty(command))
        {
            return;
        }

        if (string.Equals(command, "/cancel", StringComparison.OrdinalIgnoreCase))
        {
            await _fsmStorage.ClearAsync(from.Id);
            _logger.LogInformation("FSM session cleared by /cancel for user {UserId}", from.Id);
            await client.SendMessage(
                new ChatId(chat.Id),
                "Текущая операция отменена.",
                cancellationToken: cancellationToken);
            return;
        }

        var handler = _handlers.FirstOrDefault(h => h.CanHandle(command));
        if (handler is null)
        {
            _logger.LogWarning("Unknown command {Command} from user {UserId}", command, from.Id);
            await client.SendMessage(
                new ChatId(chat.Id),
                "Команда не поддерживается. Попробуйте /start.",
                cancellationToken: cancellationToken);
            return;
        }

        await handler.HandleAsync(client, update, command, args, cancellationToken);
    }
}
