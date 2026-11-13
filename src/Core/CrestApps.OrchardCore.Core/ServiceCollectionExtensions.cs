using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.SensitiveData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Core;

/// <summary>
/// Provides extension methods for service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the catalogs.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCatalogs(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICatalog<>), typeof(Catalog<>));
        services.TryAddScoped(typeof(INamedCatalog<>), typeof(NamedCatalog<>));
        services.TryAddScoped(typeof(ISourceCatalog<>), typeof(SourceCatalog<>));
        services.TryAddScoped(typeof(INamedSourceCatalog<>), typeof(NamedSourceCatalog<>));

        return services;
    }

    /// <summary>
    /// Adds the <see cref="SignedTokenService"/> and the <see cref="SiteUrlGenerator"/>, which create the signed,
    /// time-limited links a site sends to its customers.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSignedLinks(this IServiceCollection services)
    {
        services.TryAddSingleton<SignedTokenService>();
        services.TryAddScoped<SiteUrlGenerator>();

        return services;
    }

    /// <summary>
    /// Adds the <see cref="ISensitiveValueProtector"/>, which protects the sensitive values records store, such as
    /// bank account numbers and tax numbers, with the tenant's data protection keys.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSensitiveValueProtector(this IServiceCollection services)
    {
        services.TryAddSingleton<ISensitiveValueProtector, DataProtectionSensitiveValueProtector>();

        return services;
    }
}
