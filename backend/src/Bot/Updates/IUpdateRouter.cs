using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Updates;

public interface IUpdateRouter
{
    Task HandleAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken);
}
