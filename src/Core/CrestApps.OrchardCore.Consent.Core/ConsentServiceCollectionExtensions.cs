using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the consent service and its sources.
/// </summary>
public static class ConsentServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="IConsentService"/>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddConsent(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IConsentService, DefaultConsentService>();

        return services;
    }

    /// <summary>
    /// Adds the <see cref="IConsentService"/> and a consent source, once.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddConsentSource<TSource>(this IServiceCollection services)
        where TSource : class, IConsentSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddConsent();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IConsentSource, TSource>());

        return services;
    }
}
