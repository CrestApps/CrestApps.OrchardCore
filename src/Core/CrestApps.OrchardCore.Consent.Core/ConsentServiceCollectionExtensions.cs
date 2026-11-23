using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core;
using CrestApps.OrchardCore.Consent.Core.Models;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.Core;
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

    /// <summary>
    /// Adds the store of opt-outs for one purpose recorded against an address, and its consent source. The host
    /// registers the index provider and the migration of <see cref="ConsentAddressOptOut"/>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddConsentAddressOptOuts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IConsentAddressOptOutStore, ConsentAddressOptOutStore>();
        services.AddConsentSource<AddressOptOutConsentSource>();

        return services;
    }

    /// <summary>
    /// Adds the <see cref="IUnsubscribeLinks"/> that create and read the signed links of the e-mail preferences page.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddUnsubscribeLinks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSignedLinks();
        services.TryAddScoped<IUnsubscribeLinks, UnsubscribeLinks>();

        return services;
    }
}
