using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Bot.Handlers;

public sealed class TemplateCommandHandler : HandlerBase, ICommandHandler
{
    public TemplateCommandHandler(
        IFsmStorage fsmStorage,
        IConfiguration configuration,
        ILogger<TemplateCommandHandler> logger)
        : base(fsmStorage, configuration, logger)
    {
    }

    public bool CanHandle(string command) =>
        string.Equals(command, "/template", StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(ITelegramBotClient client, Update update, string command, string? args, CancellationToken cancellationToken)
    {
        await ReplyAsync(
            client,
            GetChatId(update),
            "Шаблоны импорта появятся на этапе 5.",
            cancellationToken);
    }
}
