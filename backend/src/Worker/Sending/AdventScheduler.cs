using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Worker.Sending;

public sealed class AdventScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdventScheduler> _logger;

    public AdventScheduler(IServiceScopeFactory scopeFactory, ILogger<AdventScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var tickStart = DateTime.UtcNow;
            _logger.LogInformation("Delivery tick start {UtcNow}", tickStart);
            var sw = Stopwatch.StartNew();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var deliveryService = scope.ServiceProvider.GetRequiredService<DeliveryService>();
                await deliveryService.RunScheduledDeliveryTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delivery tick failed");
            }
            finally
            {
                sw.Stop();
                _logger.LogInformation("Delivery tick end {UtcNow}; duration {DurationMs} ms", DateTime.UtcNow, sw.Elapsed.TotalMilliseconds);
            }

            try
            {
                var now = DateTime.UtcNow;
                var millisecondsIntoMinute = (now.Second * 1000) + now.Millisecond;
                var delayMs = 60000 - millisecondsIntoMinute;
                if (delayMs <= 0)
                {
                    delayMs = 1000;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
