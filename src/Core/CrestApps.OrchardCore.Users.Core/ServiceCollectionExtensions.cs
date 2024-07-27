using CrestApps.OrchardCore.Users.Core.Handlers;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Users.Handlers;

namespace CrestApps.OrchardCore.Users.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection TryAddBasicDisplayNameProvider(this IServiceCollection services)
    {
        services.TryAddScoped<IDisplayNameProvider, DefaultDisplayNameProvider>();

        return services;
    }

    public static IServiceCollection AddDisplayNameProvider(this IServiceCollection services)
    {
        services.AddScoped<IDisplayNameProvider, DisplayNameProvider>();

        return services;
    }

    public static IServiceCollection AddUserCacheService(this IServiceCollection services)
    {
        services.AddScoped<IUserCacheService, DefaultUserCacheService>();
        services.AddScoped<IUserEventHandler, UserComponentsEventHandler>();

        return services;
    }
}
