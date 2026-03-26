using DeviceDataManager.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceDataManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDeviceDataManagerInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ModuleConfigurationState>();
        services.AddSingleton<ICountReadRepository, PostgresCountReadRepository>();
        services.AddHostedService<EdgeModuleHostedService>();
        return services;
    }
}
