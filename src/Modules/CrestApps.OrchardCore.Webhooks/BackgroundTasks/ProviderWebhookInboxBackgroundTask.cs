using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

// Orchard keys a background task's saved settings (its schedule, and whether it is on) by the task's full type name.
// This task moved here from Contact Center, so it keeps its original namespace: a schedule an admin saved for it keeps
// applying after the upgrade.
namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Retries provider webhook inbox messages whose immediate persisted dispatch did not complete. Each due
/// message is isolated in its own fresh child scope so a poison delivery never blocks the rest of the batch.
/// </summary>
/// <remarks>
/// A pass only runs when every registered <see cref="IProviderWebhookInboxDispatchGate"/> admits it: Contact Center
/// holds it back while its Voice feature drains, as it did when the inbox was part of Contact Center.
/// </remarks>
[BackgroundTask(
    Title = "Provider Webhook Inbox Dispatch",
    Schedule = "* * * * *",
    Description = "Processes durable provider webhook deliveries with bounded retries and dead-lettering.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class ProviderWebhookInboxBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => DoWorkAsync(serviceProvider, run, cancellationToken));

    private static async Task DoWorkAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var leases = new List<IDisposable>();

        try
        {
            foreach (var gate in serviceProvider.GetServices<IProviderWebhookInboxDispatchGate>())
            {
                var lease = gate.TryEnter();

                if (lease is null)
                {
                    // A feature holding the inbox back, such as Contact Center Voice while it drains.
                    run.SkipForLease();

                    return;
                }

                leases.Add(lease);
            }

            await DispatchAsync(serviceProvider, run, cancellationToken);
        }
        finally
        {
            foreach (var lease in leases)
            {
                lease.Dispose();
            }
        }
    }

    private static async Task DispatchAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var inbox = serviceProvider.GetRequiredService<IProviderWebhookInbox>();
        var logger = serviceProvider.GetRequiredService<ILogger<ProviderWebhookInboxBackgroundTask>>();

        try
        {
            var processed = await inbox.DispatchDueAsync(cancellationToken);

            run.AddItems(processed);

            if (processed > 0 && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Processed {Count} provider webhook inbox message(s).", processed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Swallowed so the next pass runs, but no longer invisible: the run is reported as failed.
            run.ReportError(exception);

            logger.LogError(
                exception,
                "An error occurred while dispatching the provider webhook inbox.");
        }
    }
}
