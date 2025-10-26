using System;
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
            _logger.LogInformation("Delivery tick start {UtcNow}", DateTime.UtcNow);

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

            _logger.LogInformation("Delivery tick end {UtcNow}", DateTime.UtcNow);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
