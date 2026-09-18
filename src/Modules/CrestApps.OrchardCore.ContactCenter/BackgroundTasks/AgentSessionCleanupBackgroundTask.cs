using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Signs out agents whose real-time session heartbeat has gone stale so routing stops targeting a client
/// that is no longer connected. Acts as the safety net behind the SignalR disconnect handler.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Agent Session Cleanup",
    Schedule = "* * * * *",
    Description = "Expires agent sessions whose heartbeat has gone stale and signs the agent out.",
    LockTimeout = 5_000,
    LockExpiration = 60_000)]
public sealed class AgentSessionCleanupBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IAgentSessionCleanupCycle>().RunAsync(cancellationToken);
}
