using CrestApps.Core.ContactCenter;
using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

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
public sealed class OrphanedActivityRecoveryCycle : IOrphanedActivityRecoveryCycle
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

    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IOrphanedActivityRecoveryService _recoveryService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrphanedActivityRecoveryCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="recoveryService">The recovery service.</param>
    /// <param name="logger">The logger.</param>
    public OrphanedActivityRecoveryCycle(
        IContactCenterFeatureWorkManager workManager,
        IOrphanedActivityRecoveryService recoveryService,
        ILogger<OrphanedActivityRecoveryCycle> logger)
    {
        _workManager = workManager;
        _recoveryService = recoveryService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterCapabilities.Queues);

        if (workLease is null)
        {
            return;
        }


        var recovered = await _recoveryService.RecoverAsync(
            TimeSpan.FromMinutes(GracePeriodMinutes),
            MaxRecoveriesPerRun,
            cancellationToken);

        if (recovered > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Recovered {RecoveredCount} orphaned Contact Center activity(ies) stranded in an intermediate status.",
                recovered);
        }
    }
}
