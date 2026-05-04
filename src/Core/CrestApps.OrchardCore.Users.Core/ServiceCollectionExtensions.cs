using CrestApps.OrchardCore.Users.Core.Handlers;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Users.Handlers;

namespace CrestApps.OrchardCore.Users.Core;

/// <summary>
/// Provides extension methods for service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Performs the try add basic display name provider operation.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection TryAddBasicDisplayNameProvider(this IServiceCollection services)
    {
        services.TryAddScoped<IDisplayNameProvider, DefaultDisplayNameProvider>();

        return services;
    }

    /// <summary>
    /// Adds the display name provider.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddDisplayNameProvider(this IServiceCollection services)
    {
        services.AddScoped<IDisplayNameProvider, DisplayNameProvider>();

        return services;
    }

    /// <summary>
    /// Adds the user cache service.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddUserCacheService(this IServiceCollection services)
    {
        services.AddScoped<IUserCacheService, DefaultUserCacheService>();
        services.AddScoped<IUserEventHandler, UserComponentsEventHandler>();

        return services;
    }
}
