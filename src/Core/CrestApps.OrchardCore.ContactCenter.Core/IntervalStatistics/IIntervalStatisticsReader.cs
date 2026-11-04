namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// Reads the 15-minute interval statistics of queues and agents. Each call reads whole days through the day index,
/// never the interactions.
/// </summary>
public interface IIntervalStatisticsReader
{
    /// <summary>
    /// Gets the queues' intervals that start inside a range.
    /// </summary>
    /// <param name="queueIds">The queues, or <see langword="null"/> for every queue.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range, exclusive.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The intervals, in order.</returns>
    Task<IReadOnlyList<QueueIntervalStatistic>> GetQueueIntervalsAsync(IEnumerable<string> queueIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the agents' intervals that start inside a range.
    /// </summary>
    /// <param name="userIds">The agents' users, or <see langword="null"/> for every agent.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range, exclusive.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The intervals, in order.</returns>
    Task<IReadOnlyList<AgentIntervalStatistic>> GetAgentIntervalsAsync(IEnumerable<string> userIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the days computed inside a range, so a caller can tell a day without work from a day not computed yet.
    /// </summary>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range, exclusive.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The days.</returns>
    Task<IReadOnlyList<IntervalStatisticsDay>> GetDaysAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}
