using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Core.Signatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;

namespace CrestApps.OrchardCore.Webhooks.Core;

/// <summary>
/// Registers the provider webhook inbox.
/// </summary>
public static class ProviderWebhookInboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the durable provider webhook inbox, its store, the scope executor it isolates deliveries with, the
    /// default provider name normalizer and the built-in signature validators. The feature that hosts the inbox adds
    /// the index provider, the schema migration and the background task that retries due deliveries.
    /// </summary>
    /// <param name="services">The tenant service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddProviderWebhookInbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(ProviderWebhookInboxStorage.CollectionName));

        services.TryAddScoped<IProviderWebhookInboxStore, ProviderWebhookInboxStore>();
        services.TryAddScoped<IProviderWebhookInbox, ProviderWebhookInbox>();
        services.TryAddScoped<IProviderWebhookInboxScopeExecutor, ShellProviderWebhookInboxScopeExecutor>();
        services.TryAddSingleton<IWebhookProviderNameNormalizer, DefaultWebhookProviderNameNormalizer>();
        services.AddWebhookSignatureValidator(HmacWebhookSignatureValidator.Sha256Hex);
        services.AddWebhookSignatureValidator(HmacWebhookSignatureValidator.Sha1Base64);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebhookSignatureValidator, SharedKeyWebhookSignatureValidator>());

        return services;
    }

    /// <summary>
    /// Registers a signature validator once, however many features add it.
    /// </summary>
    /// <param name="services">The tenant service collection.</param>
    /// <param name="validator">The validator.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWebhookSignatureValidator(this IServiceCollection services, IWebhookSignatureValidator validator)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validator);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IWebhookSignatureValidator) && ReferenceEquals(descriptor.ImplementationInstance, validator)))
        {
            services.AddSingleton(validator);
        }

        return services;
    }
}
