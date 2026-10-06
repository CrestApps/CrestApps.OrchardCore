using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Supplies what a dialer profile's recent calls measured, which predictive dialing sizes its over-dial from.
/// Implementations read a durable source shared by every node, so all nodes pace from the same numbers.
/// </summary>
public interface IDialerPacingStatisticsProvider
{
    /// <summary>
    /// Gets the pacing statistics of a dialer profile over a rolling window ending now.
    /// </summary>
    /// <param name="dialerProfileId">The identifier of the dialer profile to measure.</param>
    /// <param name="window">How far back to measure.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The statistics, or <see langword="null"/> when they cannot be determined, which predictive dialing treats as a
    /// reason not to over-dial.
    /// </returns>
    Task<DialerPacingStatistics> GetStatisticsAsync(string dialerProfileId, TimeSpan window, CancellationToken cancellationToken = default);
}
