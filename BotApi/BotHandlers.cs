using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NuhhlerAdventBot.BotApi;

public class BotHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotConfig _botConfig;
    private CancellationToken _cancellationToken;

    public BotHandlers(ITelegramBotClient botClient, BotConfig botConfig)
    {
        _botClient = botClient;
        _botConfig = botConfig;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        if (update.Type == UpdateType.Message && update.Message?.Text != null)
        {
            var message = update.Message;
            var chatId = message.Chat.Id;
            var messageText = message.Text;
            var fromId = message.From.Id;

            // Separate command and argument
            var commandParts = messageText.Split(' ', 2);
            var command = commandParts[0].ToLower();
            var argument = commandParts.Length > 1 ? commandParts[1] : null;

            switch (command)
            {
                case "/start":
                    await botClient.SendMessage(
                        chatId,
                        "Добро пожаловать! Я адвент-календарь бот.",
                        cancellationToken: _cancellationToken
                    );
                    break;

                case "/advent":
                    await HandleAdventCommand(chatId, cancellationToken);
                    break;

                case "/set":
                case "/edit":
                case "/delete":
                case "/check":
                    if (IsAdmin(fromId))
                        await HandleAdminCommand(command, argument, chatId, cancellationToken);
                    else
                        await botClient.SendMessage(
                            chatId,
                            "У вас нет прав для выполнения этой команды.",
                            cancellationToken: _cancellationToken
                        );
                    break;

                default:
                    await botClient.SendMessage(
                        chatId,
                        "Извините, я понимаю только команды.",
                        cancellationToken: _cancellationToken
                    );
                    break;
            }
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }

    private bool IsAdmin(long userId)
    {
        return userId == _botConfig.AdminId;
    }

    private async Task HandleAdventCommand(long chatId, CancellationToken cancellationToken)
    {
        // TODO: Add /advent command logic
        await _botClient.SendMessage(
            chatId,
            "Команда /advent пока не реализована.",
            cancellationToken: _cancellationToken
        );
    }

    private async Task HandleAdminCommand(string command, string argument, long chatId,
        CancellationToken cancellationToken)
    {
        switch (command)
        {
            case "/set":
                await HandleSetCommand(argument, chatId);
                break;
            case "/edit":
                await HandleEditCommand(argument, chatId);
                break;
            case "/delete":
                await HandleDeleteCommand(argument, chatId);
                break;
            case "/check":
                await HandleCheckCommand(argument, chatId);
                break;
        }
    }

    // Admin commands

    private async Task HandleSetCommand(string argument, long chatId)
    {
        // TODO: Реализовать логику команды /set
        await _botClient.SendMessage(
            chatId,
            $"Команда /set с аргументом '{argument}' пока не реализована.",
            cancellationToken: _cancellationToken
        );
    }

    private async Task HandleEditCommand(string argument, long chatId)
    {
        // TODO: Add /edit command logic
        await _botClient.SendMessage(
            chatId,
            $"Команда /edit с аргументом '{argument}' пока не реализована.",
            cancellationToken: _cancellationToken
        );
    }

    private async Task HandleDeleteCommand(string argument, long chatId)
    {
        // TODO: Add /delete command logic
        await _botClient.SendMessage(
            chatId,
            $"Команда /delete с аргументом '{argument}' пока не реализована.",
            cancellationToken: _cancellationToken
        );
    }

    private async Task HandleCheckCommand(string argument, long chatId)
    {
        // TODO: Add /check command logic
        await _botClient.SendMessage(
            chatId,
            $"Команда /check с аргументом '{argument}' пока не реализована.",
            cancellationToken: _cancellationToken
        );
    }
}