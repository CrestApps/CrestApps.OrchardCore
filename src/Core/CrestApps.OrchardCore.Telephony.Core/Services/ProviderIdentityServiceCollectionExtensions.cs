using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Registers the provider identity resolver.
/// </summary>
public static class ProviderIdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers the provider identity resolver once, and makes it the provider name normalizer of the provider webhook
    /// inbox, so a delivery from an aliased provider is keyed by the provider's canonical name. It replaces the inbox's
    /// pass-through default whichever feature registers first.
    /// </summary>
    /// <param name="services">The tenant service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddProviderIdentityResolver(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IProviderIdentityResolver, ProviderIdentityResolver>();
        services.Replace(ServiceDescriptor.Singleton<IWebhookProviderNameNormalizer>(
            serviceProvider => serviceProvider.GetRequiredService<IProviderIdentityResolver>()));

        return services;
    }
}
