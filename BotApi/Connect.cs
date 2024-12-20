using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NuhhlerAdventBot.BotApi;

public class Connect
{
    internal async Task StartBot()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false)
            .Build();

        var botConfig = config.GetSection("BotConfig").Get<BotConfig>();

        var botClient = new TelegramBotClient(botConfig.BotToken);
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        var handlers = new BotHandlers(botClient, botConfig);

        var privateChatCommands = new List<BotCommand>
        {
            new BotCommand { Command = "set", Description = "Установить" },
            new BotCommand { Command = "edit", Description = "Редактировать" },
            new BotCommand { Command = "delete", Description = "Удалить" },
            new BotCommand { Command = "check", Description = "Проверить" }
        };

        await botClient.SetMyCommands(
            commands: privateChatCommands,
            scope: new BotCommandScopeAllPrivateChats(),
            cancellationToken: cancellationToken
        );

        Console.WriteLine("Команды для приватных чатов установлены.");

        var groupChatCommands = new List<BotCommand>
        {
            new BotCommand { Command = "advent", Description = "Получить своё адвент-посллание" }
        };

        await botClient.SetMyCommands(
            commands: groupChatCommands,
            scope: new BotCommandScopeAllGroupChats(),
            cancellationToken: cancellationToken
        );

        Console.WriteLine("Команды для групповых чатов установлены.");
        
        botClient.StartReceiving(
            handlers.HandleUpdateAsync,
            handlers.HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );

        var me = await botClient.GetMe(cancellationToken);
        Console.WriteLine($"Бот @{me.Username} запущен и ожидает сообщений.");

        Console.ReadLine();
        cts.Cancel();
    }
}