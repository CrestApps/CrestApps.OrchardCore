using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Sends the webhook deliveries that are due, each saved as soon as it is attempted.
/// </summary>
[BackgroundTask(
    Title = "AI Webhook Deliveries",
    Schedule = "* * * * *",
    Description = "Sends queued AI webhook events and retries failed ones.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class AIWebhookDeliveryBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The most deliveries one pass sends.
    /// </summary>
    internal const int DeliveriesPerPass = 100;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var deliveryStore = serviceProvider.GetRequiredService<IAIWebhookDeliveryStore>();
        var subscriptionStore = serviceProvider.GetRequiredService<IAIWebhookSubscriptionStore>();
        var sender = serviceProvider.GetRequiredService<AIWebhookSender>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIWebhookDeliveryBackgroundTask>>();
        var subscriptions = new Dictionary<string, AIWebhookSubscription>(StringComparer.Ordinal);

        foreach (var delivery in await deliveryStore.ListDueAsync(clock.UtcNow, DeliveriesPerPass, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (!subscriptions.TryGetValue(delivery.SubscriptionId, out var subscription))
                {
                    subscription = await subscriptionStore.FindByIdAsync(delivery.SubscriptionId, cancellationToken);
                    subscriptions[delivery.SubscriptionId] = subscription;
                }

                await sender.SendAsync(delivery, subscription, cancellationToken);
                await deliveryStore.UpdateAsync(delivery, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Webhook delivery '{DeliveryId}' could not be sent.", delivery.ItemId.SanitizeLogValue());
            }
        }
    }
}
