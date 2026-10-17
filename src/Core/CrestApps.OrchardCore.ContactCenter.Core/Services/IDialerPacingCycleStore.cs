using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Stores the compact record of each dialer pacing cycle.
/// </summary>
public interface IDialerPacingCycleStore : ICatalog<DialerPacingCycle>
{
    /// <summary>
    /// Finds the record of the cycle of a campaign queue that ended last, optionally among the given outcomes only.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    /// <param name="outcomes">The outcomes to consider, or <see langword="null"/> to consider every outcome.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The record, or <see langword="null"/> when there is none.</returns>
    Task<DialerPacingCycle> FindLatestByQueueAsync(
        string queueId,
        IEnumerable<DialerPacingCycleOutcome> outcomes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the record of the cycle run with a dialer profile that ended last, optionally among the given outcomes only.
    /// </summary>
    /// <param name="dialerProfileId">The dialer profile.</param>
    /// <param name="outcomes">The outcomes to consider, or <see langword="null"/> to consider every outcome.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The record, or <see langword="null"/> when there is none.</returns>
    Task<DialerPacingCycle> FindLatestByProfileAsync(
        string dialerProfileId,
        IEnumerable<DialerPacingCycleOutcome> outcomes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the records whose latest cycle ended within a period, oldest first.
    /// </summary>
    /// <param name="fromUtc">The start of the period, inclusive.</param>
    /// <param name="toUtc">The end of the period, exclusive.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyCollection<DialerPacingCycle>> GetCompletedBetweenAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}
