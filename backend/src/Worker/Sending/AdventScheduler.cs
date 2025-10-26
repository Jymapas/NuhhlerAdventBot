using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Worker.Sending;

public sealed class AdventScheduler : BackgroundService
{
    private readonly DeliveryService _deliveryService;
    private readonly ILogger<AdventScheduler> _logger;

    public AdventScheduler(DeliveryService deliveryService, ILogger<AdventScheduler> logger)
    {
        _deliveryService = deliveryService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Delivery tick start {UtcNow}", DateTime.UtcNow);

            try
            {
                await _deliveryService.RunScheduledDeliveryTickAsync(stoppingToken);
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
