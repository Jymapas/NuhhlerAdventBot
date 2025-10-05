using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Background;

public class StartupWarmupService(IServiceProvider sp, ILogger<StartupWarmupService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Infrastructure.Persistence.SqliteBootstrapper.EnsurePragmas(db);
        await db.Database.MigrateAsync(stoppingToken);

        // прогрев индексов
        _ = await db.Notes.AsNoTracking().OrderByDescending(n => n.UpdatedAt).Take(1).CountAsync(stoppingToken);

        log.LogInformation("Warmup finished");
    }
}
