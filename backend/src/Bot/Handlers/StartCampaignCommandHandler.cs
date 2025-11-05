using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class StartCampaignCommandHandler : HandlerBase, ICommandHandler
{
    public StartCampaignCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<StartCampaignCommandHandler> logger)
        : base(fsmStorage, configuration, logger)
    {
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/start_campaign", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        var userId = GetUserId(update);
        var username = GetUsername(update);
        if (userId is null)
        {
            Logger.LogWarning("Start campaign command without user id.");
            return;
        }

        if (!IsOwner(userId.Value, username))
        {
            await ReplyAsync(
                client,
                GetChatId(update),
                "У вас нет прав использовать эту команду.",
                cancellationToken);
            return;
        }

        await ReplyAsync(
            client,
            GetChatId(update),
            "Запуск кампании будет на этапе 8.",
            cancellationToken);
    }
}
