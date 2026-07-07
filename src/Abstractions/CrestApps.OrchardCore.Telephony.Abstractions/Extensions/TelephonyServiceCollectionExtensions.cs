using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telephony.Extensions;

/// <summary>
/// Provides extension methods to register telephony providers with the dependency injection container.
/// </summary>
public static class TelephonyServiceCollectionExtensions
{
    /// <summary>
    /// Registers a telephony provider and marks it as enabled.
    /// </summary>
    /// <typeparam name="T">The provider type implementing <see cref="ITelephonyProvider"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The technical name used to identify the provider.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTelephonyProvider<T>(this IServiceCollection services, string name)
        where T : class, ITelephonyProvider
    {
        services.Configure<TelephonyProviderOptions>(options =>
        {
            options.TryAddProvider(name, new TelephonyProviderTypeOptions(typeof(T))
            {
                IsEnabled = true,
            });
        });

        return services;
    }

    /// <summary>
    /// Registers a configuration that contributes one or more telephony providers based on the
    /// current tenant settings.
    /// </summary>
    /// <typeparam name="TConfiguration">The configuration type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTelephonyProviderOptionsConfiguration<TConfiguration>(this IServiceCollection services)
        where TConfiguration : class, IConfigureOptions<TelephonyProviderOptions>
    {
        services.AddTransient<IConfigureOptions<TelephonyProviderOptions>, TConfiguration>();

        return services;
    }
}
