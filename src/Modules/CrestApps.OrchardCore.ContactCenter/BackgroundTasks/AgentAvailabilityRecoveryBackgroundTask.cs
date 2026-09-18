using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Recovers Contact Center agents whose after-call work was orphaned or exceeded its deadline.
/// <para>
/// The recovery pass is bounded by a hard wall-clock budget enforced through a linked
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that cancels in-flight work, and that
/// budget is kept safely below the distributed-lock expiration so a slow pass can never outlive its lock and let a
/// second node begin an overlapping recovery pass while agent-state transitions are still being written. Work that
/// does not fit in the budget is recovered on the following tick.
/// </para>
/// </summary>
[BackgroundTask(
    Title = "Contact Center Agent Availability Recovery",
    Schedule = "* * * * *",
    Description = "Recovers agent capacity from orphaned or expired after-call work.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class AgentAvailabilityRecoveryBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire, which
    /// guarantees the next scheduled tick cannot start an overlapping recovery pass on another node.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IAgentAvailabilityRecoveryCycle>().RunAsync(cancellationToken);
}
