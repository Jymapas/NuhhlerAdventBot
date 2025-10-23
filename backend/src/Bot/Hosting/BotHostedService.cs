using Bot.Fsm;
using Bot.Updates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Env;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace Bot.Hosting;

public sealed class BotHostedService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly ILogger<BotHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUpdateRouter _updateRouter;
    private readonly IFsmStorage _fsmStorage;

    public BotHostedService(
        ILogger<BotHostedService> logger,
        IConfiguration configuration,
        IUpdateRouter updateRouter,
        IFsmStorage fsmStorage)
    {
        _logger = logger;
        _configuration = configuration;
        _updateRouter = updateRouter;
        _fsmStorage = fsmStorage;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration[EnvKeys.BotToken];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("BOT_TOKEN not provided; Telegram polling disabled.");
            await WaitForShutdownAsync(stoppingToken);
            return;
        }

        var botClient = new TelegramBotClient(token);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.CallbackQuery,
                UpdateType.MyChatMember
            }
        };

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        botClient.StartReceiving(
            async (client, update, ct) =>
            {
                try
                {
                    await _updateRouter.HandleAsync(client, update, ct);
                }
                catch (OperationCanceledException)
                {
                    // shutdown requested
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process update {UpdateType}", update.Type);
                }
            },
            async (client, exception, ct) =>
            {
                if (exception is not OperationCanceledException)
                {
                    _logger.LogError(exception, "Telegram long polling error");
                }

                await Task.CompletedTask;
            },
            receiverOptions,
            cancellationToken: linkedCts.Token);

        _logger.LogInformation("Telegram long polling started.");

        try
        {
            await _fsmStorage.CleanupExpiredAsync();
            while (!stoppingToken.IsCancellationRequested)
            {
                await _fsmStorage.CleanupExpiredAsync();
                await Task.Delay(CleanupInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            linkedCts.Cancel();
            _logger.LogInformation("Telegram long polling stopped.");
        }
    }

    private static async Task WaitForShutdownAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }
}
