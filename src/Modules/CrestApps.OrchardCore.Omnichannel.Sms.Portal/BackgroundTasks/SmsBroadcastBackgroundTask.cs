using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.BackgroundTasks;

/// <summary>
/// Periodically fans out queued SMS broadcasts. Each recipient becomes an individual 1:1 thread; progress is
/// persisted after every recipient so a restart resumes without re-sending. The sweep is a no-op when there
/// are no queued or in-progress broadcasts.
/// </summary>
[BackgroundTask(
    Title = "SMS Broadcast Fan-out",
    Schedule = "* * * * *",
    Description = "Sends queued SMS broadcasts to their recipients as individual 1:1 threads, resuming safely after a restart.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class SmsBroadcastBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ISmsBroadcastCycle>().RunAsync(cancellationToken);
}
