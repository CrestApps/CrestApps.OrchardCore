using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Runs one pacing cycle for each enabled dialer profile so power and progressive campaigns dial automatically.
/// <para>
/// Each run is bounded by a wall-clock budget enforced both by a between-profile deadline check and a hard
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that cancels in-flight work, and that
/// budget is kept safely below the distributed-lock expiration so a slow run can never outlive its lock and let a
/// second node begin an overlapping pacing cycle. Profiles that do not fit in the budget are simply paced on the
/// following tick.
/// </para>
/// </summary>
[BackgroundTask(
    Title = "Contact Center Dialer Pacing",
    Schedule = "* * * * *",
    Description = "Reserves agents and places outbound calls for enabled dialer profiles.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class DialerPacingBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire, which
    /// guarantees the next scheduled tick cannot start an overlapping pacing cycle on another node.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IDialerPacingCycle>().RunAsync(cancellationToken);
}
