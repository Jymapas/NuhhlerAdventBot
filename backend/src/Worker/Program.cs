using Application.Abstractions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();
SerilogBootstrap.ConfigureSerilog(builder);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<TickerService>();

var app = builder.Build();

var info = app.Services.GetRequiredService<IBotInfo>();
Console.WriteLine($"Advent Bot (Worker) started; version: {info.Version}");
await app.RunAsync();

public sealed class TickerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"[Worker] tick {DateTime.UtcNow:O}");
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
