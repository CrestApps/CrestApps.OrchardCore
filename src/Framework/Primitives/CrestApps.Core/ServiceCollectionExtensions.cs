using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCrestAppsCoreServices(this IServiceCollection services)
    {
        services.TryAddScoped<IODataValidator, ODataFilterValidator>();

        return services;
    }
}
