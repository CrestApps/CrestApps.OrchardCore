using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Supplies the rolling outbound-dialing statistics an abandonment policy needs to decide whether a dialer
/// profile stays within its configured cap. Implementations are provider-neutral and read from a durable,
/// distributed-safe source so the rate is consistent across nodes.
/// </summary>
public interface IDialerAbandonmentStatisticsProvider
{
    /// <summary>
    /// Gets the rolling abandonment statistics for a dialer profile over the supplied window.
    /// </summary>
    /// <param name="dialerProfileId">The identifier of the dialer profile to measure.</param>
    /// <param name="window">The rolling window over which to measure abandonment.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// The measured statistics, or <see langword="null"/> when the statistics cannot be determined, which
    /// the policy treats as a fail-closed signal.
    /// </returns>
    Task<DialerAbandonmentStatistics> GetStatisticsAsync(string dialerProfileId, TimeSpan window, CancellationToken cancellationToken = default);
}
