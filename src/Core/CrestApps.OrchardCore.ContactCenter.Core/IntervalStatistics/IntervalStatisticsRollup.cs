namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// Rolls 15-minute queue intervals up into longer intervals, across queues and channels.
/// </summary>
public static class IntervalStatisticsRollup
{
    /// <summary>
    /// Combines intervals into intervals of a longer length.
    /// </summary>
    /// <param name="intervals">The 15-minute intervals.</param>
    /// <param name="minutes">The length to roll up to: 15, 30 or 60.</param>
    /// <param name="queueId">The queue identifier the combined intervals carry, or <see langword="null"/>.</param>
    /// <returns>One interval per start, in order.</returns>
    public static IReadOnlyList<QueueIntervalStatistic> Combine(IEnumerable<QueueIntervalStatistic> intervals, int minutes = IntervalStatisticsConstants.IntervalMinutes, string queueId = null)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        if (minutes is not (15 or 30 or 60))
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), minutes, "Intervals roll up to 15, 30 or 60 minutes.");
        }

        var combined = new SortedDictionary<DateTime, QueueIntervalStatistic>();

        foreach (var interval in intervals)
        {
            var start = IntervalStatisticsConstants.IntervalStart(interval.IntervalStartUtc, minutes);

            if (!combined.TryGetValue(start, out var total))
            {
                total = new QueueIntervalStatistic
                {
                    QueueId = queueId,
                    Channel = interval.Channel,
                    IntervalStartUtc = start,
                    IntervalMinutes = minutes,
                };

                combined.Add(start, total);
            }

            total.Add(interval);
        }

        return [.. combined.Values];
    }
}
