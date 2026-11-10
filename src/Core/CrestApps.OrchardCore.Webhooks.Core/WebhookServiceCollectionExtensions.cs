using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;

namespace CrestApps.OrchardCore.Webhooks.Core;

/// <summary>
/// Registers the outbound webhooks building block.
/// </summary>
public static class WebhookServiceCollectionExtensions
{
    /// <summary>
    /// Registers what one signed webhook POST needs: the signer, the sender and its egress-guarded HTTP client. A feature
    /// that posts to a URL of its own (the Send Signed Webhook workflow task) calls only this.
    /// </summary>
    /// <param name="services">The tenant service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWebhookSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IWebhookSender)))
        {
            return services;
        }

        services.TryAddSingleton<IWebhookSigner, HmacSha256WebhookSigner>();
        services.TryAddScoped<IWebhookSender, WebhookSender>();

        // Receivers are other systems: the egress guard refuses hosts on the site's own network, when they are called
        // and not only when they are saved, and redirects are not followed.
        services.AddEgressGuardedHttpClient(WebhookStorage.HttpClientName);

        return services;
    }

    /// <summary>
    /// Registers outbound webhooks: the subscription and delivery stores, the event catalog, the publisher and the
    /// outbox dispatcher. The Webhooks feature calls it; it adds the schema, the background task and the admin.
    /// </summary>
    /// <param name="services">The tenant service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWebhooks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddWebhookSender();

        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(WebhookStorage.CollectionName));
        services.TryAddScoped<IWebhookSubscriptionStore, WebhookSubscriptionStore>();
        services.TryAddScoped<IWebhookDeliveryStore, WebhookDeliveryStore>();
        services.TryAddScoped<IWebhookEventCatalog, WebhookEventCatalog>();
        services.TryAddScoped<IWebhookPublisher, WebhookPublisher>();
        services.TryAddScoped<IWebhookOutbox, WebhookOutbox>();
        services.TryAddScoped<WebhookDispatcher>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWebhookEventProvider, OptionsWebhookEventProvider>());

        return services;
    }
}
