using AgentSquad.Agents.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSquad.Agents.Maintenance;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMaintenanceSquad(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentSquadOptions>().Bind(configuration.GetSection(AgentSquadOptions.SectionName)).ValidateDataAnnotations();
        services.AddHttpClient("TokenFactory");
        services.AddHttpClient("PackageSecurity");
        services.AddSingleton<MaintenanceRunStore>();
        services.AddSingleton<MaintenancePolicy>();
        services.AddSingleton<IPackageSecurityLookup, PackageSecurityLookup>();
        services.AddSingleton<IMaintenanceModelProbe, TokenFactoryModelProbe>();
        services.AddSingleton<IMaintenanceNarrator, TokenFactoryNarrator>();
        services.AddSingleton<MaintenanceService>();
        services.AddHostedService<MaintenanceScheduler>();
        return services;
    }
}
