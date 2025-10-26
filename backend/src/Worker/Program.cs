using Application.Abstractions;
using Infrastructure.Persistence;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Env;
using Shared.Logging;
using Telegram.Bot;
using Worker.DailyBrief;
using Worker.Sending;
using Microsoft.EntityFrameworkCore;

EnvFileLoader.LoadFromAncestors();

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();
SerilogBootstrap.ConfigureSerilog(builder);

builder.Services.AddInfrastructure(builder.Configuration);

var token = builder.Configuration[EnvKeys.BotToken];
if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("BOT_TOKEN is required for the worker to send messages.");

builder.Services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(token));
builder.Services.AddHostedService<AdventScheduler>();
builder.Services.AddHostedService<DailyBriefService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

var info = app.Services.GetRequiredService<IBotInfo>();
Console.WriteLine($"Advent Bot (Worker) started; version: {info.Version}");
await app.RunAsync();
