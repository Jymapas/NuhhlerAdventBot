using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
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

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        var handlers = new BotHandlers(botClient, botConfig);

        botClient.StartReceiving(
            handlers.HandleUpdateAsync,
            handlers.HandleErrorAsync,
            receiverOptions,
            cts.Token
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Бот @{me.Username} запущен и ожидает сообщений.");

        Console.ReadLine();
        cts.Cancel();
    }
}