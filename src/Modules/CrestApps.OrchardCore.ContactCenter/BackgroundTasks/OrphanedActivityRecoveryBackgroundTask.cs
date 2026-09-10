using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Recovers activities stranded in an intermediate routing status (Reserved, Dialing, AwaitingAgentResponse,
/// AwaitingCustomerAnswer, or InProgress) whose reservation, interaction, and agent state were already released.
/// Such a record is no longer a waiting queue item and is not tied to any agent, so nothing re-offers it and
/// nothing surfaces it - it just inflates the campaign's "in progress" count and can never be worked. This task
/// returns those orphans to a workable state without ever re-dialing a customer who may already have been reached.
/// It participates in the Queues feature's work-admission drain so it stops admitting work while that feature is
/// quiescing, honours the cancellation token so it stops promptly on shutdown, and only recovers a record once it
/// has been stale for <see cref="GracePeriodMinutes"/> minutes - comfortably longer than any reservation ring
/// window or call-setup time - so it can never race a live call.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Orphaned Activity Recovery",
    Schedule = "* * * * *",
    Description = "Recovers activities stuck in an intermediate status after their reservation and call were released.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class OrphanedActivityRecoveryBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// How long an intermediate-status record must have been stale before it is eligible for recovery. Kept well
    /// above any reservation ring window or call-setup time so a genuinely live (or slow) call is never touched.
    /// </summary>
    private const int GracePeriodMinutes = 10;

    /// <summary>
    /// The maximum number of orphaned activities recovered in a single run, so a large backlog is drained in
    /// bounded batches over successive ticks instead of in one unbounded pass.
    /// </summary>
    private const int MaxRecoveriesPerRun = 200;

    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.Queues);

        if (workLease is null)
        {
            return;
        }

        var recoveryService = serviceProvider.GetRequiredService<IOrphanedActivityRecoveryService>();
        var logger = serviceProvider.GetRequiredService<ILogger<OrphanedActivityRecoveryBackgroundTask>>();

        var recovered = await recoveryService.RecoverAsync(
            TimeSpan.FromMinutes(GracePeriodMinutes),
            MaxRecoveriesPerRun,
            cancellationToken);

        if (recovered > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Recovered {RecoveredCount} orphaned Contact Center activity(ies) stranded in an intermediate status.",
                recovered);
        }
    }
}
