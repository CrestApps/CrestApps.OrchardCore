using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Redelivers Contact Center domain events whose handler dispatch previously failed. It is the durable
/// retry mechanism behind <see cref="IContactCenterOutbox"/>, so a transient handler failure no longer
/// silently drops an event.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Event Outbox Dispatch",
    Schedule = "* * * * *",
    Description = "Retries Contact Center domain events whose handler dispatch failed, with exponential back-off and dead-lettering.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class OutboxDispatchBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var outbox = serviceProvider.GetRequiredService<IContactCenterOutbox>();
        var logger = serviceProvider.GetRequiredService<ILogger<OutboxDispatchBackgroundTask>>();

        try
        {
            var redelivered = await outbox.DispatchDueAsync(cancellationToken);

            if (redelivered > 0 && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Redelivered {Count} Contact Center event(s) from the outbox.", redelivered);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while dispatching the Contact Center event outbox.");
        }
    }
}
