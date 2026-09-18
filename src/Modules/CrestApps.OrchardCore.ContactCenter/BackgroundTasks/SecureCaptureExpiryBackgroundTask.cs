using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Expires secure capture sessions whose customer window has elapsed without a submission, so a capture a
/// customer never completed is settled and any recording pause it engaged is resumed rather than left suppressing
/// capture indefinitely.
/// <para>
/// The pass is bounded by a hard wall-clock budget enforced through a linked
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that stays safely below the
/// distributed-lock expiration, so a slow pass cannot outlive its lock and let a second node begin an overlapping
/// expiry pass. Work that does not fit in the budget is settled on the following tick.
/// </para>
/// </summary>
[BackgroundTask(
    Title = "Contact Center Secure Capture Expiry",
    Schedule = "* * * * *",
    Description = "Expires secure capture sessions whose customer window elapsed without a submission.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class SecureCaptureExpiryBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    /// <summary>
    /// The maximum number of captures a single pass settles, bounding the unit of work per tick.
    /// </summary>
    private const int MaxCapturesPerRun = 200;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ISecureCaptureExpiryCycle>().RunAsync(cancellationToken);
}
