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
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        services.AddSingleton<ICameraRepository, CameraRepository>();
        services.AddSingleton<ICaptureRepository, CaptureRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();

        return services;
    }
}
