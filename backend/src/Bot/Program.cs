using System.Runtime.InteropServices;
using Application.Abstractions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shared.Env;
using Shared.Logging;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();
SerilogBootstrap.ConfigureSerilog(builder);

builder.Services.AddInfrastructure();

var botToken = builder.Configuration[EnvKeys.BotToken] ?? "";
if (string.IsNullOrWhiteSpace(botToken))
    Console.WriteLine("WARNING: BOT_TOKEN is empty; Bot will run without Telegram connectivity.");

builder.Services.AddSingleton(new TelegramBotClient(botToken));

var app = builder.Build();

var info = app.Services.GetRequiredService<IBotInfo>();
Console.WriteLine($"Advent Bot (Bot) started; version: {info.Version}; OS: {RuntimeInformation.OSDescription}");

await app.RunAsync();
