using System.Globalization;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace Shared.Logging;

public static class SerilogBootstrap
{
    public static void ConfigureSerilog(HostApplicationBuilder builder, string? explicitPath = null, string logName = "app")
    {
        var configuration = builder.Configuration;
        var logPath = ResolveLogPath(explicitPath, configuration, logName);

        EnsureDirectoryExists(logPath);

        var retention = ParseRetention(configuration["LOG_RETENTION_DAYS"]);
        var minimumLevel = ParseLogLevel(configuration["SERILOG_MIN_LEVEL"]);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithExceptionDetails()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retention,
                buffered: true,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {Level:u3} {Message:lj} | props: {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        builder.Services.AddLogging(logging => logging.AddSerilog(dispose: true));
    }

    private static string ResolveLogPath(string? explicitPath, IConfiguration configuration, string logName)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var fallback = configuration["LOG_PATH"];
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return Path.Combine("logs", logName.Equals("app", StringComparison.OrdinalIgnoreCase) ? "app.log" : $"{logName}.log");
    }

    private static void EnsureDirectoryExists(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch
        {
            // если не удалось создать каталог, пробуем оставить Serilog обрабатывать исключение самостоятельно
        }
    }

    private static int? ParseRetention(string? retentionRaw)
    {
        if (string.IsNullOrWhiteSpace(retentionRaw))
        {
            return 14;
        }

        if (int.TryParse(retentionRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retention) && retention > 0)
        {
            return retention;
        }

        return 14;
    }

    private static LogEventLevel ParseLogLevel(string? minLevelRaw)
    {
        if (!string.IsNullOrWhiteSpace(minLevelRaw) && Enum.TryParse<LogEventLevel>(minLevelRaw, true, out var level))
        {
            return level;
        }

        return LogEventLevel.Information;
    }
}
