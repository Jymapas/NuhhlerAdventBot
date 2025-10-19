using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Shared.Logging;

public static class SerilogBootstrap
{
    public static void ConfigureSerilog(HostApplicationBuilder builder)
    {
        var logPath = builder.Configuration["LOG_PATH"] ?? "logs/app.log";
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, buffered: true)
            .CreateLogger();

        builder.Services.AddLogging(l => l.AddSerilog(dispose: true));
    }
}
