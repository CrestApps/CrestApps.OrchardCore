using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreModelServices(this IServiceCollection services)
    {
        services
            .AddCoreModelStores()
            .AddScoped(typeof(IModelManager<>), typeof(ModelManager<>))
            .AddScoped(typeof(INamedModelManager<>), typeof(NamedModelManager<>))
            .AddScoped(typeof(ISourceModelManager<>), typeof(SourceModelManager<>))
            .AddScoped(typeof(INamedSourceModelManager<>), typeof(NamedSourceModelManager<>));

        return services;
    }

    public static IServiceCollection AddCoreModelStores(this IServiceCollection services)
    {
        services
            .AddScoped(typeof(IModelStore<>), typeof(ModelStore<>))
            .AddScoped(typeof(INamedModelStore<>), typeof(NamedModelStore<>))
            .AddScoped(typeof(ISourceModelStore<>), typeof(SourceModelStore<>))
            .AddScoped(typeof(INamedSourceModelStore<>), typeof(NamedSourceModelStore<>));

        return services;
    }
}
