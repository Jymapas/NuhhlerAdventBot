using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class NewAdventCommandHandler : HandlerBase, ICommandHandler
{
    public NewAdventCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<NewAdventCommandHandler> logger)
        : base(fsmStorage, configuration, logger)
    {
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/new_advent", StringComparison.OrdinalIgnoreCase);

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
            "Создание кампании доступно на следующем этапе.",
            cancellationToken);
    }
}
