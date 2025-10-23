using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Commands;

public interface ICommandHandler
{
    bool CanHandle(string command);
    Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken);
}
