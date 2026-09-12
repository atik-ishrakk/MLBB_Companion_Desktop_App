using Microsoft.Extensions.DependencyInjection;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;

namespace MLBB.Core.Extensions;

/// <summary>
/// Service collection extension for registering MLBB Core business services.
/// </summary>
public static class CoreServiceExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IHeroDataService, HeroDataService>();
        services.AddSingleton<IRoiConfigurationService, RoiConfigurationService>();
        services.AddSingleton<IProcessManagerService, ProcessManagerService>();
        services.AddSingleton<IAdbService, AdbService>();
        services.AddSingleton<IPhaseDetectionService, PhaseDetectionService>();
        services.AddSingleton<IDraftRecommenderService, DraftRecommenderService>();
        services.AddSingleton<IDraftAnalysisService, DraftAnalysisService>();
        services.AddSingleton<ISimulationStateService, SimulationStateService>();

        return services;
    }
}

