using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using MLBB.Core.Interfaces;
using MLBB.Infrastructure.Repositories;
using MLBB.Infrastructure.Screen;
using MLBB.Infrastructure.Vision;

namespace MLBB.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency Injection extension registering MLBB Infrastructure services and repositories.
/// </summary>
[SupportedOSPlatform("windows")]
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IHeroRepository, HeroRepository>();
        services.AddSingleton<IRoiRepository, RoiRepository>();
        services.AddSingleton<IPhaseAnchorStorageService, PhaseAnchorStorageService>();
        services.AddSingleton<IScreenCaptureProvider, ScreenCaptureProvider>();
        services.AddSingleton<IVisionEngine, OpenCvVisionEngine>();

        return services;
    }
}

