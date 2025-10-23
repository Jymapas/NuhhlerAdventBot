using Bot.Fsm;
using Bot.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Bot.Handlers.Common;

public abstract class HandlerBase
{
    protected HandlerBase(IFsmStorage fsmStorage, IConfiguration configuration, ILogger logger)
    {
        FsmStorage = fsmStorage;
        Configuration = configuration;
        Logger = logger;
    }

    protected IFsmStorage FsmStorage { get; }
    protected IConfiguration Configuration { get; }
    protected ILogger Logger { get; }

    protected static long? GetUserId(Update update) =>
        update.Message?.From?.Id;

    protected static string? GetUsername(Update update) =>
        update.Message?.From?.Username;

    protected static long GetChatId(Update update) =>
        update.Message?.Chat.Id
        ?? throw new InvalidOperationException("Message chat is required for command handlers.");

    public static (string command, string? args) ParseCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (string.Empty, null);

        var trimmed = text.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
            return (string.Empty, null);

        var index = trimmed.IndexOf(' ');
        if (index < 0)
            return (trimmed.ToLowerInvariant(), null);

        var command = trimmed[..index].ToLowerInvariant();
        var args = trimmed[(index + 1)..].Trim();
        return (command, string.IsNullOrWhiteSpace(args) ? null : args);
    }

    protected Task ReplyAsync(
        ITelegramBotClient client,
        long chatId,
        string text,
        CancellationToken cancellationToken) =>
        ReplyAsync(client, chatId, text, (InlineKeyboardMarkup?)null, cancellationToken);

    protected Task ReplyAsync(
        ITelegramBotClient client,
        long chatId,
        string text,
        InlineKeyboardMarkup? replyMarkup,
        CancellationToken cancellationToken) =>
        client.SendMessage(
            new ChatId(chatId),
            text,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);

    protected internal static string GetUsernameLink(User? user)
    {
        if (user is null)
            return "неизвестный пользователь";

        if (!string.IsNullOrWhiteSpace(user.Username))
            return $"@{user.Username}";

        if (!string.IsNullOrWhiteSpace(user.FirstName) && !string.IsNullOrWhiteSpace(user.LastName))
            return $"{user.FirstName} {user.LastName}";

        if (!string.IsNullOrWhiteSpace(user.FirstName))
            return user.FirstName;

        return user.Id.ToString();
    }

    protected bool EnsureOwner(long userId, string? username)
    {
        var isOwner = OwnerGuard.IsOwner(userId, Configuration);
        if (!isOwner)
        {
            Logger.LogWarning("User {UserId} ({Username}) is not in OWNER_IDS", userId, username);
        }

        return isOwner;
    }
}
