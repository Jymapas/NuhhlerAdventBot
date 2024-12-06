﻿using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NuhhlerAdventBot.BotApi;

public class BotHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotConfig _botConfig;

    public BotHandlers(ITelegramBotClient botClient, BotConfig botConfig)
    {
        _botClient = botClient;
        _botConfig = botConfig;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
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
                    await botClient.SendTextMessageAsync(
                        chatId,
                        "Добро пожаловать! Я адвент-календарь бот.",
                        cancellationToken: cancellationToken
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
                        await botClient.SendTextMessageAsync(
                            chatId,
                            "У вас нет прав для выполнения этой команды.",
                            cancellationToken: cancellationToken
                        );
                    break;

                default:
                    await botClient.SendTextMessageAsync(
                        chatId,
                        "Извините, я понимаю только команды.",
                        cancellationToken: cancellationToken
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
        await _botClient.SendTextMessageAsync(
            chatId,
            "Команда /advent пока не реализована.",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleAdminCommand(string command, string argument, long chatId,
        CancellationToken cancellationToken)
    {
        switch (command)
        {
            case "/set":
                await HandleSetCommand(argument, chatId, cancellationToken);
                break;
            case "/edit":
                await HandleEditCommand(argument, chatId, cancellationToken);
                break;
            case "/delete":
                await HandleDeleteCommand(argument, chatId, cancellationToken);
                break;
            case "/check":
                await HandleCheckCommand(argument, chatId, cancellationToken);
                break;
        }
    }

    // Admin commands

    private async Task HandleSetCommand(string argument, long chatId, CancellationToken cancellationToken)
    {
        // TODO: Реализовать логику команды /set
        await _botClient.SendTextMessageAsync(
            chatId,
            $"Команда /set с аргументом '{argument}' пока не реализована.",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleEditCommand(string argument, long chatId, CancellationToken cancellationToken)
    {
        // TODO: Add /edit command logic
        await _botClient.SendTextMessageAsync(
            chatId,
            $"Команда /edit с аргументом '{argument}' пока не реализована.",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleDeleteCommand(string argument, long chatId, CancellationToken cancellationToken)
    {
        // TODO: Add /delete command logic
        await _botClient.SendTextMessageAsync(
            chatId,
            $"Команда /delete с аргументом '{argument}' пока не реализована.",
            cancellationToken: cancellationToken
        );
    }

    private async Task HandleCheckCommand(string argument, long chatId, CancellationToken cancellationToken)
    {
        // TODO: Add /check command logic
        await _botClient.SendTextMessageAsync(
            chatId,
            $"Команда /check с аргументом '{argument}' пока не реализована.",
            cancellationToken: cancellationToken
        );
    }
}