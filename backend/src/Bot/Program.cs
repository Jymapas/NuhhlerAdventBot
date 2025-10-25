using System.IO;
using Application.Abstractions;
using Bot.Callbacks;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers;
using Bot.Handlers.Callbacks;
using Bot.Hosting;
using Bot.Updates;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Env;
using Shared.Logging;
using Serilog;

try
{
    LoadEnvironmentFromEnvFile();

    var builder = Host.CreateApplicationBuilder(args);

    builder.Configuration.AddEnvironmentVariables();
    SerilogBootstrap.ConfigureSerilog(builder);

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSingleton<IFsmStorage, InMemoryFsmStorage>();
    builder.Services.AddSingleton<IUpdateRouter, UpdateRouter>();
    builder.Services.AddSingleton<CommandDispatcher>();
    builder.Services.AddSingleton<ICommandDispatcher>(sp => sp.GetRequiredService<CommandDispatcher>());
    builder.Services.AddSingleton<CallbackDispatcher>();
    builder.Services.AddSingleton<ICallbackDispatcher>(sp => sp.GetRequiredService<CallbackDispatcher>());

    builder.Services.AddSingleton<ICommandHandler, StartCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, NewAdventCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, SetRecipientCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, TemplateCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, ImportCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, CheckCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, EditCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, StartCampaignCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, PauseResumeCommandHandler>();
    builder.Services.AddSingleton<ICommandHandler, TodayCommandHandler>();

    builder.Services.AddSingleton<ICallbackHandler, CommandCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, CheckRangesCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, CheckDayCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, EditTextCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, EditTimeCallbackHandler>();

    builder.Services.AddHostedService<BotHostedService>();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bot.Program");

    var botToken = app.Services.GetRequiredService<IConfiguration>()[EnvKeys.BotToken];
    if (string.IsNullOrWhiteSpace(botToken))
    {
        logger.LogWarning("BOT_TOKEN is missing; Telegram long polling will not start.");
    }

    var info = app.Services.GetRequiredService<IBotInfo>();
    logger.LogInformation("Advent Bot (Bot) starting; version: {Version}", info.Version);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Advent Bot (Bot) terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void LoadEnvironmentFromEnvFile()
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
