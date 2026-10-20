using CrestApps.OrchardCore.Commerce.Webhooks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Commerce.Webhooks.BackgroundTasks;

/// <summary>
/// Every minute, sends the webhook deliveries that are due again, and once a day lets go of the log's old entries.
/// </summary>
[BackgroundTask(
    Title = "Webhook deliveries",
    Schedule = "* * * * *",
    Description = "Sends webhook deliveries that failed again, with backoff, and deletes delivered and failed ones after 30 days.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class WebhookDeliveriesBackgroundTask : IBackgroundTask
{
    // At up to 15 seconds a send, a batch fits well inside the lock.
    private const int BatchSize = 20;

    internal static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<WebhookDeliveriesBackgroundTask>>();

        try
        {
            var now = serviceProvider.GetRequiredService<IClock>().UtcNow;
            var store = serviceProvider.GetRequiredService<IWebhookDeliveryStore>();
            var dispatcher = serviceProvider.GetRequiredService<WebhookDispatcher>();
            var session = serviceProvider.GetRequiredService<global::YesSql.ISession>();
            var due = (await store.ListDueAsync(now, BatchSize, cancellationToken)).Select(delivery => delivery.ItemId).ToArray();

            // Each is saved as soon as it's sent, so a run that stops half way, or the next one, never sends it again.
            // A save detaches what was loaded before it, so each is loaded afresh.
            foreach (var id in due)
            {
                if (await store.FindByIdAsync(id, cancellationToken) is { Status: Models.WebhookDeliveryStatus.Pending } delivery)
                {
                    await dispatcher.SendAsync(delivery, cancellationToken);
                    await session.SaveChangesAsync(cancellationToken);
                }
            }

            // The log is kept for a month; one batch a minute keeps up with any store.
            await store.DeleteOlderThanAsync(now - Retention, BatchSize, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "An error occurred while sending webhook deliveries.");
        }
    }
}
