using Bot.Fsm;
using Bot.Updates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Env;
using Shared.Logging;
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
        try
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
                    var actor = update.Message?.From ?? update.CallbackQuery?.From ?? update.MyChatMember?.From;
                    using var scope = LogScopes.WithUpdate(update.Id, actor?.Id, actor?.Username);

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
                        _logger.LogError(ex, "Update handling failed");
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
        catch (OperationCanceledException)
        {
            // shutdown requested
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Bot hosted service encountered a critical error");
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
