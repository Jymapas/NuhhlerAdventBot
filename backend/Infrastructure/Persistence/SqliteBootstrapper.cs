using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

public static class SqliteBootstrapper
{
    public static IServiceCollection AddSqlite(this IServiceCollection services, IConfiguration cfg)
    {
        var path = cfg["DATABASE_PATH"] ?? "./data/app.db";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite(new SqliteConnectionStringBuilder { DataSource = path }.ToString()));

        return services;
    }

    public static void EnsurePragmas(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        db.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
    }
}
