using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Retries provider webhook inbox messages whose immediate persisted dispatch did not complete. Each due
/// message is isolated in its own fresh child scope so a poison delivery never blocks the rest of the batch.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Provider Webhook Inbox Dispatch",
    Schedule = "* * * * *",
    Description = "Processes durable provider webhook deliveries with bounded retries and dead-lettering.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class ProviderWebhookInboxBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IProviderWebhookInboxCycle>().RunAsync(cancellationToken);
}
