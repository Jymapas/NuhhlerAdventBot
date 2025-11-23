using System;
using System.Threading.Tasks;
using Application.Abstractions;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Env;
using Shared.Logging;
using Serilog;
using Telegram.Bot;
using Worker.DailyBrief;
using Worker.Sending;

try
{
    EnvFileLoader.LoadFromAncestors();

    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddEnvironmentVariables();

    SerilogBootstrap.ConfigureSerilog(builder, builder.Configuration["WORKER_LOG_PATH"], "worker");
    GlobalExceptionHandler.Register("Worker");

    builder.Services.AddInfrastructure(builder.Configuration);

    var token = builder.Configuration[EnvKeys.BotToken];
    if (string.IsNullOrWhiteSpace(token))
    {
        throw new InvalidOperationException("BOT_TOKEN is required for the worker to send messages.");
    }

    builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(token));
    builder.Services.AddHostedService<AdventScheduler>();
    builder.Services.AddHostedService<DailyBriefService>();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Worker.Program");

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    var info = app.Services.GetRequiredService<IBotInfo>();
    logger.LogInformation("Advent Bot (Worker) starting; version: {Version}", info.Version);

    try
    {
        await app.RunAsync();
    }
    catch (Exception runEx)
    {
        logger.LogCritical(runEx, "Worker host terminated unexpectedly");
        Log.Fatal(runEx, "Advent Bot (Worker) terminated unexpectedly");
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Advent Bot (Worker) failed during initialization");
}
finally
{
    Log.CloseAndFlush();
}
