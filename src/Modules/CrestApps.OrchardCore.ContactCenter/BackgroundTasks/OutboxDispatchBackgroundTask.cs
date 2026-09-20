using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Redelivers Contact Center domain events whose handler dispatch previously failed. It is the durable
/// retry mechanism behind <see cref="IContactCenterOutbox"/>, so a transient handler failure no longer
/// silently drops an event. Each due message is isolated in its own fresh child scope so a poison message
/// never blocks the rest of the batch.
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
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IOutboxDispatchCycle>().RunAsync(cancellationToken);
}
