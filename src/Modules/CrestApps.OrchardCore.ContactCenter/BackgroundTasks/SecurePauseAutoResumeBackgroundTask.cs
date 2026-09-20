using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Force-resumes recordings that have stayed paused past the tenant's maximum secure-pause window, so a
/// sensitive-data pause that was never explicitly resumed cannot silently suppress capture for the remainder of a
/// compliance-recorded call.
/// <para>
/// The pass is bounded by a hard wall-clock budget enforced through a linked
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that stays safely below the
/// distributed-lock expiration, so a slow pass cannot outlive its lock and let a second node begin an overlapping
/// resume pass. Work that does not fit in the budget is resumed on the following tick.
/// </para>
/// </summary>
[BackgroundTask(
    Title = "Contact Center Secure Pause Auto-Resume",
    Schedule = "* * * * *",
    Description = "Resumes recordings paused past the configured maximum secure-pause window.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class SecurePauseAutoResumeBackgroundTask : IBackgroundTask
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

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ISecurePauseAutoResumeCycle>().RunAsync(cancellationToken);
}
