using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Application.Abstractions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Env;
using Shared.Logging;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

LoadEnvironmentFromEnvFile();
builder.Configuration.AddEnvironmentVariables();
SerilogBootstrap.ConfigureSerilog(builder);

builder.Services.AddInfrastructure();

var botToken = builder.Configuration[EnvKeys.BotToken];
if (!TryConfigureTelegramBotClient(builder.Services, botToken))
{
    Console.WriteLine("WARNING: BOT_TOKEN is missing or invalid; Bot will run without Telegram connectivity.");
}

var app = builder.Build();

var info = app.Services.GetRequiredService<IBotInfo>();
Console.WriteLine($"Advent Bot (Bot) started; version: {info.Version}; OS: {RuntimeInformation.OSDescription}");

await app.RunAsync();

bool TryConfigureTelegramBotClient(IServiceCollection services, string? token)
{
    if (string.IsNullOrWhiteSpace(token))
        return false;

    // Telegram tokens have format "<bot_id>:<hash>"
    if (!Regex.IsMatch(token, @"^\d+:[\w-]+$"))
        return false;

    services.AddSingleton(_ => new TelegramBotClient(token));
    return true;
}

void LoadEnvironmentFromEnvFile()
{
    var current = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(current))
    {
        var envPath = Path.Combine(current, ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                var key = trimmed[..separatorIndex].Trim();
                if (string.IsNullOrEmpty(key))
                    continue;

                var rawValue = trimmed[(separatorIndex + 1)..].Trim();
                var value = rawValue.Trim('\'', '"');

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    Environment.SetEnvironmentVariable(key, value);
            }

            break;
        }

        current = Directory.GetParent(current)?.FullName;
    }
}
