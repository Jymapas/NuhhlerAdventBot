using Application;
using Application.Abstractions;
using Application.Import;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? cfg = null)
    {
        services.AddSingleton<IBotInfo, BotInfo>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var configuration = cfg ?? sp.GetRequiredService<IConfiguration>();
            var dbPath = configuration["DB_PATH"] ?? "data/advent.db";
            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddScoped<IAdventRepository, AdventRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeliveryLogRepository, DeliveryLogRepository>();
        services.AddScoped<IImportParser, ImportParser>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<ITemplateGenerator, TemplateGenerator>();

        return services;
    }
}
