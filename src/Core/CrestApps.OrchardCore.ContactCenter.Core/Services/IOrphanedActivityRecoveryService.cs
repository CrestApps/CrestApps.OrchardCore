namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Recovers activities that are stranded in an intermediate routing status (Reserved, Dialing,
/// AwaitingAgentResponse, AwaitingCustomerAnswer, or InProgress) after the reservation, interaction, and agent
/// state that once bound them have already been released. Such an activity is no longer a waiting queue item and
/// is not tied to any agent, so nothing re-offers it and nothing surfaces it - it simply inflates the campaign's
/// "in progress" count and can never be worked. This service finds those orphans and returns them to a workable
/// state, without ever re-dialing a customer who may already have been reached.
/// </summary>
public interface IOrphanedActivityRecoveryService
{
    /// <summary>
    /// Recovers up to <paramref name="maxToRecover"/> orphaned activities whose intermediate status has been
    /// stale for at least <paramref name="gracePeriod"/>. A candidate is only recovered once it is confirmed to
    /// have no live reservation and no unsettled interaction, so a genuinely live (or slow) call is never
    /// touched. Records that were provably never answered are returned to <c>Pending</c> and re-queued for
    /// re-offer; records that may have connected are moved to a terminal <c>Failed</c> status instead of being
    /// dialed again.
    /// </summary>
    /// <param name="gracePeriod">How long an intermediate-status record must have been stale before it is
    /// eligible for recovery. Must be comfortably longer than any reservation ring window or call-setup time so
    /// the sweep cannot race a live call.</param>
    /// <param name="maxToRecover">The maximum number of activities to recover in one pass.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of activities recovered.</returns>
    Task<int> RecoverAsync(TimeSpan gracePeriod, int maxToRecover, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds, without changing anything, the activities <see cref="RecoverAsync"/> would act on: those stranded in an
    /// intermediate status for at least <paramref name="gracePeriod"/> that have no live reservation, no unsettled call and
    /// no after-call work waiting for an agent. A manager reads this to see stuck work before the sweep reaches it.
    /// </summary>
    /// <param name="gracePeriod">How long an intermediate-status record must have been stale to be listed.</param>
    /// <param name="maxToFind">The most activities to examine.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The orphaned activities, with what recovery would do with each.</returns>
    Task<IReadOnlyList<OrphanedActivity>> FindAsync(TimeSpan gracePeriod, int maxToFind, CancellationToken cancellationToken = default);
}

/// <summary>
/// An activity stranded in an intermediate status that recovery would act on.
/// </summary>
/// <param name="ActivityItemId">The activity's identifier.</param>
/// <param name="CampaignId">The campaign it belongs to, or <see langword="null"/>.</param>
/// <param name="Status">The status it is stranded in.</param>
/// <param name="ReservedUtc">When it was last reserved, or <see langword="null"/>.</param>
/// <param name="WouldRequeue"><see langword="true"/> when it was provably never answered and recovery would put it back in the
/// queue; <see langword="false"/> when it may have reached the customer and recovery would fail it rather than dial it again.</param>
public sealed record OrphanedActivity(string ActivityItemId, string CampaignId, string Status, DateTime? ReservedUtc, bool WouldRequeue);
