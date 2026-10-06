using System.Collections.Concurrent;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Keeps each dialer profile's most recent pacing statistics for a few seconds, so pacing that runs every couple of
/// seconds, on every campaign a profile dials, measures the profile once rather than on every cycle.
/// </summary>
/// <remarks>
/// One instance serves the whole tenant. A cached value is a finished snapshot that is never changed after it is stored,
/// so sharing it between scopes and threads is safe.
/// </remarks>
public sealed class DialerPacingStatisticsCache
{
    private readonly ConcurrentDictionary<(string ProfileId, TimeSpan Window), Entry> _entries = new();

    /// <summary>
    /// Gets the statistics stored for a profile and window, when they are still fresh.
    /// </summary>
    /// <param name="dialerProfileId">The dialer profile.</param>
    /// <param name="window">The window the statistics cover.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="statistics">The stored statistics.</param>
    /// <returns><see langword="true"/> when fresh statistics were found.</returns>
    public bool TryGet(string dialerProfileId, TimeSpan window, DateTime nowUtc, out DialerPacingStatistics statistics)
    {
        if (_entries.TryGetValue((dialerProfileId, window), out var entry) && entry.ExpiresUtc > nowUtc)
        {
            statistics = entry.Statistics;

            return true;
        }

        statistics = null;

        return false;
    }

    /// <summary>
    /// Stores the statistics measured for a profile and window.
    /// </summary>
    /// <param name="dialerProfileId">The dialer profile.</param>
    /// <param name="window">The window the statistics cover.</param>
    /// <param name="statistics">The statistics.</param>
    /// <param name="expiresUtc">When the statistics stop being fresh.</param>
    public void Set(string dialerProfileId, TimeSpan window, DialerPacingStatistics statistics, DateTime expiresUtc)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        _entries[(dialerProfileId, window)] = new Entry(statistics, expiresUtc);
    }

    private sealed record Entry(DialerPacingStatistics Statistics, DateTime ExpiresUtc);
}
