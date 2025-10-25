using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Callbacks;

public interface ICallbackDispatcher
{
    Task DispatchAsync(ITelegramBotClient client, CallbackQuery callbackQuery, CancellationToken cancellationToken);
}

public sealed class CallbackDispatcher : ICallbackDispatcher
{
    private readonly IEnumerable<ICallbackHandler> _handlers;
    private readonly ILogger<CallbackDispatcher> _logger;

    public CallbackDispatcher(IEnumerable<ICallbackHandler> handlers, ILogger<CallbackDispatcher> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public async Task DispatchAsync(ITelegramBotClient client, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is not { Length: > 0 } data)
        {
            await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        var handler = _handlers.FirstOrDefault(h => h.CanHandle(data));
        if (handler is null)
        {
            _logger.LogWarning("Unhandled callback data {Data}", data);
            await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            return;
        }

        await handler.HandleAsync(client, callbackQuery, cancellationToken);
        await client.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
    }
}

public sealed class CommandCallbackHandler : ICallbackHandler
{
    private const string Prefix = "cmd:";

    private readonly Bot.Fsm.IFsmStorage _fsmStorage;
    private readonly ILogger<CommandCallbackHandler> _logger;

    public CommandCallbackHandler(Bot.Fsm.IFsmStorage fsmStorage, ILogger<CommandCallbackHandler> logger)
    {
        _fsmStorage = fsmStorage;
        _logger = logger;
    }

    public bool CanHandle(string data) => data.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
        {
            _logger.LogWarning("Callback without message context for data {Data}", callbackQuery.Data);
            return;
        }

        var command = callbackQuery.Data![Prefix.Length..];
        var text = command switch
        {
            "/cancel" => null,
            "/check" => "Команда /check готовится — введите её вручную, чтобы получить заглушку.",
            "/import" => "Команда /import переведёт вас в режим импорта файла.",
            "/start_campaign" => "Команда /start_campaign запустит кампанию (заглушка).",
            "/today" => "Команда /today покажет превью дня (заглушка).",
            _ => $"Команда {command} пока не поддерживается из клавиатуры."
        };

        if (string.Equals(command, "/cancel", StringComparison.OrdinalIgnoreCase))
        {
            await _fsmStorage.ClearAsync(callbackQuery.From.Id);
            await client.SendMessage(
                new ChatId(callbackQuery.Message.Chat.Id),
                "Текущая операция отменена.",
                cancellationToken: cancellationToken);
            _logger.LogInformation("FSM session cleared via inline cancel for user {UserId}", callbackQuery.From.Id);
            return;
        }

        if (text is null)
        {
            _logger.LogWarning("Callback {Data} produced no response", callbackQuery.Data);
            return;
        }

        await client.SendMessage(
            new ChatId(callbackQuery.Message.Chat.Id),
            text,
            cancellationToken: cancellationToken);
    }
}
