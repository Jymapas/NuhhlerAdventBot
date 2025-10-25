using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Callbacks;

public interface ICallbackHandler
{
    bool CanHandle(string data);
    Task HandleAsync(ITelegramBotClient client, CallbackQuery callbackQuery, CancellationToken cancellationToken);
}
