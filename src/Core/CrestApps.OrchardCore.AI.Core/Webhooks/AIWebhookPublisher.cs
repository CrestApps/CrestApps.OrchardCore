using CrestApps.Core.Support;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Webhooks;

/// <summary>
/// Publishes AI events (<see cref="AIWebhookEvents"/>) through the Webhooks building block. The AI Webhooks feature
/// declares the events (<see cref="AIWebhookEventProvider"/>); the subscriptions, the signing, the delivery outbox and
/// the delivery log are the Webhooks feature's.
/// </summary>
public static class AIWebhookPublisher
{
    /// <summary>
    /// The data protector purpose the secrets of the AI webhook subscriptions were stored with, before they moved to the
    /// Webhooks building block. Only the upgrade that moves them reads it.
    /// </summary>
    public const string ProtectorPurpose = "CrestApps.OrchardCore.AI.Webhooks";

    /// <summary>
    /// Queues an AI event when the Webhooks feature is on; does nothing otherwise. Never throws.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="eventName">The event, from <see cref="AIWebhookEvents"/>.</param>
    /// <param name="data">What happened.</param>
    /// <returns>A task that completes when the event is queued.</returns>
    public static async Task TryPublishAsync(IServiceProvider services, string eventName, object data)
    {
        var publisher = services?.GetService<IWebhookPublisher>();

        if (publisher is null)
        {
            return;
        }

        try
        {
            // The publisher sends only declared events, so with the AI Webhooks feature off nothing is sent.
            await publisher.PublishAsync(eventName, data);
        }
        catch (Exception ex)
        {
            services.GetService<ILoggerFactory>()?.CreateLogger(typeof(AIWebhookPublisher))
                .LogWarning(ex, "The '{EventName}' webhook event could not be queued.", eventName.SanitizeLogValue());
        }
    }
}
