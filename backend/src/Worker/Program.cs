using Application.Abstractions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Env;
using Shared.Logging;
using Telegram.Bot;
using Worker.DailyBrief;
using Worker.Sending;

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

var info = app.Services.GetRequiredService<IBotInfo>();
Console.WriteLine($"Advent Bot (Worker) started; version: {info.Version}");
await app.RunAsync();
