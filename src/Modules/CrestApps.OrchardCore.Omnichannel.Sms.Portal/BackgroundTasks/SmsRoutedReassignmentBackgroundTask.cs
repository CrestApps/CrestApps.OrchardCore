using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.BackgroundTasks;

/// <summary>
/// Periodically returns routed (push-assigned) SMS conversations that the assigned agent has not picked up
/// within the grace window to their queue's shared pool, so a message never stalls in one inbox. The sweep is a
/// no-op when there are no unpicked routed conversations.
/// </summary>
[BackgroundTask(
    Title = "SMS Routed Reassignment",
    Schedule = "* * * * *",
    Description = "Returns unpicked routed SMS conversations to their queue pool so another agent can take them.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class SmsRoutedReassignmentBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ISmsRoutedReassignmentCycle>().RunAsync(cancellationToken);
}
