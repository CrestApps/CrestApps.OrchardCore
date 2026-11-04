using CrestApps.OrchardCore.AI.Core.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Lets go of the old AI webhook delivery log. AI events are sent through the Webhooks feature now; the deliveries it
/// recorded before that are deleted once they are a month old.
/// </summary>
[BackgroundTask(
    Title = "AI Webhook Deliveries",
    Schedule = "0 * * * *",
    Description = "Deletes AI webhook deliveries recorded before AI events moved to the Webhooks feature, once they are 30 days old.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class AIWebhookDeliveryBackgroundTask : IBackgroundTask
{
    internal const int DeliveriesPerPass = 100;

    internal static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var deliveryStore = serviceProvider.GetRequiredService<IAIWebhookDeliveryStore>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var session = serviceProvider.GetRequiredService<ISession>();

        if (await deliveryStore.DeleteOlderThanAsync(clock.UtcNow - Retention, DeliveriesPerPass, cancellationToken) > 0)
        {
            await session.SaveChangesAsync(cancellationToken);
        }
    }
}
