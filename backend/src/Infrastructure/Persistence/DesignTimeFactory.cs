using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        var dbPath = Environment.GetEnvironmentVariable("DB_PATH") ?? "data/advent.db";
        if (!Path.IsPathRooted(dbPath))
        {
            dbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dbPath));
        }

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        builder.UseSqlite($"Data Source={dbPath}");
        return new AppDbContext(builder.Options);
    }
}
