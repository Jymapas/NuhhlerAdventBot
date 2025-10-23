using Bot.Callbacks;
using Bot.Commands;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot.Updates;

public sealed class UpdateRouter : IUpdateRouter
{
    private readonly ILogger<UpdateRouter> _logger;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICallbackDispatcher _callbackDispatcher;

    public UpdateRouter(
        ILogger<UpdateRouter> logger,
        ICommandDispatcher commandDispatcher,
        ICallbackDispatcher callbackDispatcher)
    {
        _logger = logger;
        _commandDispatcher = commandDispatcher;
        _callbackDispatcher = callbackDispatcher;
    }

    public async Task HandleAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message when update.Message is { Text: { } text }:
            {
                if (!text.TrimStart().StartsWith("/", StringComparison.Ordinal))
                {
                    return;
                }

                var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
                var user = update.Message!.From;
                _logger.LogInformation("Command {Command} from user {UserId} ({Username})", command, user?.Id, user?.Username);

                await _commandDispatcher.DispatchAsync(botClient, update, cancellationToken);
                break;
            }

            case UpdateType.CallbackQuery when update.CallbackQuery is { } callback:
            {
                var user = callback.From;
                _logger.LogInformation("Callback {Data} from user {UserId} ({Username})", callback.Data, user.Id, user.Username);

                await _callbackDispatcher.DispatchAsync(botClient, callback, cancellationToken);
                break;
            }

            case UpdateType.MyChatMember when update.MyChatMember is { } chatMember:
            {
                var user = chatMember.From;
                _logger.LogInformation("Chat member update: status {Status}; user {UserId} ({Username})",
                    chatMember.NewChatMember.Status, user.Id, user.Username);
                break;
            }
        }
    }
}
