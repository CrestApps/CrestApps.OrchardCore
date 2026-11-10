using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Webhooks.BackgroundTasks;

/// <summary>
/// Every minute, sends the webhook deliveries that are due again, and lets go of the log's old entries.
/// </summary>
[BackgroundTask(
    Title = "Webhook deliveries",
    Schedule = "* * * * *",
    Description = "Sends webhook deliveries that failed again, with backoff, and deletes delivered and failed ones after 30 days, except the deliveries of events a feature keeps (AI events).",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class WebhookDeliveriesBackgroundTask : IBackgroundTask
{
    // At up to 15 seconds a send, a batch fits well inside the lock.
    private const int BatchSize = 20;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => SendDueAsync(serviceProvider, run, cancellationToken));

    private static async Task SendDueAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var now = serviceProvider.GetRequiredService<IClock>().UtcNow;
        var store = serviceProvider.GetRequiredService<IWebhookDeliveryStore>();
        var dispatcher = serviceProvider.GetRequiredService<WebhookDispatcher>();
        var session = serviceProvider.GetRequiredService<global::YesSql.ISession>();
        var due = (await store.ListDueAsync(now, BatchSize, cancellationToken)).Select(delivery => delivery.ItemId).ToArray();
        var deleted = 0;

        // Each is saved as soon as it's sent, so a run that stops half way, or the next one, never sends it again.
        // A save detaches what was loaded before it, so each is loaded afresh.
        foreach (var id in due)
        {
            if (await store.FindByIdAsync(id, cancellationToken) is { Status: WebhookDeliveryStatus.Pending } delivery)
            {
                await dispatcher.SendAsync(delivery, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }
        }

        // The log is kept for a month by default; one batch a minute keeps up with any store. The deliveries of the
        // events a feature keeps (AI Webhooks keeps every AI event) are never purged.
        var retention = serviceProvider.GetService<IOptions<WebhookDeliveryRetentionOptions>>()?.Value ?? new WebhookDeliveryRetentionOptions();

        if (retention.DeliveryRetention > TimeSpan.Zero)
        {
            deleted = await store.DeleteOlderThanAsync(now - retention.DeliveryRetention, BatchSize, [.. retention.KeptEventNames], cancellationToken);

            run.AddItems(deleted, "purged");
        }

        if (due.Length == 0 && deleted == 0)
        {
            run.Skip();
        }
    }
}
