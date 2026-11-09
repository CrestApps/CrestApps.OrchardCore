using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Declares webhook events from a startup.
/// </summary>
public static class WebhookEventServiceCollectionExtensions
{
    /// <summary>
    /// Declares an event webhook subscriptions can choose. Call it from a startup gated on the feature that publishes
    /// the event; a second declaration of the same name is ignored.
    /// </summary>
    /// <param name="services">The tenant service collection.</param>
    /// <param name="name">The event's name, such as <c>order.paid</c>.</param>
    /// <param name="displayName">What an administrator sees when choosing events.</param>
    /// <param name="group">What it is about, to group events by.</param>
    /// <param name="samplePayload">An example of the event's <c>data</c>, or <see langword="null"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWebhookEvent(this IServiceCollection services, string name, string displayName, string group, object samplePayload = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return services.Configure<WebhookEventOptions>(options =>
        {
            if (!options.Events.Any(entry => string.Equals(entry.Name, name, StringComparison.Ordinal)))
            {
                options.Events.Add(new WebhookEventDescriptor(name, displayName, group, samplePayload));
            }
        });
    }
}
