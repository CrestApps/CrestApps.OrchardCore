using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreModelServices(this IServiceCollection services)
    {
        services
            .AddScoped(typeof(INamedModelStore<>), typeof(NamedModelStore<>))
            .AddScoped(typeof(IModelStore<>), typeof(ModelStore<>))
            .AddScoped(typeof(INamedModelManager<>), typeof(NamedModelManager<>))
            .AddScoped(typeof(IModelManager<>), typeof(ModelManager<>));

        return services;
    }
}
