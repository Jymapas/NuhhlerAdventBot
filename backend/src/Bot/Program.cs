using System;
using System.Threading.Tasks;
using Application.Abstractions;
using Bot.Callbacks;
using Bot.Commands;
using Bot.Fsm;
using Bot.Handlers;
using Bot.Handlers.Callbacks;
using Bot.Hosting;
using Bot.Updates;
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

try
{
    EnvFileLoader.LoadFromAncestors();

    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddEnvironmentVariables();

    SerilogBootstrap.ConfigureSerilog(builder, builder.Configuration["BOT_LOG_PATH"], "bot");
    GlobalExceptionHandler.Register("Bot");

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
    builder.Services.AddSingleton<ICommandHandler, SetTimeCommandHandler>();

    builder.Services.AddSingleton<ICallbackHandler, CommandCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, CheckRangesCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, CheckDayCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, EditTextCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, EditTimeCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, DailyBriefSendNowCallbackHandler>();
    builder.Services.AddSingleton<ICallbackHandler, DailyBriefPauseCallbackHandler>();

    builder.Services.AddHostedService<BotHostedService>();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bot.Program");

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    var botToken = app.Services.GetRequiredService<IConfiguration>()[EnvKeys.BotToken];
    if (string.IsNullOrWhiteSpace(botToken))
    {
        logger.LogWarning("BOT_TOKEN is missing; Telegram long polling will not start.");
    }

    var info = app.Services.GetRequiredService<IBotInfo>();
    logger.LogInformation("Advent Bot (Bot) starting; version: {Version}", info.Version);

    try
    {
        await app.RunAsync();
    }
    catch (Exception runEx)
    {
        logger.LogCritical(runEx, "Host terminated unexpectedly");
        Log.Fatal(runEx, "Advent Bot (Bot) terminated unexpectedly");
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Advent Bot (Bot) failed during initialization");
}
finally
{
    Log.CloseAndFlush();
}
