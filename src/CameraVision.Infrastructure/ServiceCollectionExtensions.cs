using CameraVision.Core.Repositories;
using CameraVision.Infrastructure.Data;
using CameraVision.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CameraVision.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the SQLite context factory and all repositories.</summary>
    public static IServiceCollection AddCameraVisionData(this IServiceCollection services, string databasePath)
    {
        // Web and Api share this database file — the busy timeout rides out
        // cross-process write collisions (WAL is enabled by DbInitializer).
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath};Default Timeout=10"));

        services.AddSingleton<ICameraRepository, CameraRepository>();
        services.AddSingleton<ICaptureRepository, CaptureRepository>();
        services.AddSingleton<ICaptureRuleRepository, CaptureRuleRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<ICameraHealthEventRepository, CameraHealthEventRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();

        return services;
    }
}
