using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

namespace CrestApps.OrchardCore.ContactCenter.IntervalStatistics;

/// <summary>
/// How far the interval statistics are computed: the rebuild and last run, the days of the last 30 computed and final,
/// and today's totals so far. The status page and the AI tool both read it here.
/// </summary>
internal sealed class IntervalStatisticsStatus
{
    /// <summary>
    /// How long a rebuild may go without progress before it reads as stalled.
    /// </summary>
    public static readonly TimeSpan StalledAfter = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets the state: the rebuild and the last run.
    /// </summary>
    public IntervalStatisticsState State { get; init; }

    /// <summary>
    /// Gets how many of the last 30 days and today are computed.
    /// </summary>
    public int DaysComputed { get; init; }

    /// <summary>
    /// Gets how many of those days are final.
    /// </summary>
    public int DaysFinal { get; init; }

    /// <summary>
    /// Gets today's day marker, or <see langword="null"/> when today is not computed yet.
    /// </summary>
    public IntervalStatisticsDay Today { get; init; }

    /// <summary>
    /// Gets today's totals over every queue.
    /// </summary>
    public QueueIntervalStatistic TodayTotal { get; init; }

    /// <summary>
    /// Gets how many queues had work today.
    /// </summary>
    public int QueuesToday { get; init; }

    /// <summary>
    /// Gets whether a rebuild is running but has made no progress for <see cref="StalledAfter"/>.
    /// </summary>
    public bool Stalled { get; init; }

    /// <summary>
    /// Gets the first day of today, in the statistics' days.
    /// </summary>
    public DateTime TodayUtc { get; init; }

    /// <summary>
    /// Reads the status.
    /// </summary>
    /// <param name="roller">The roller, for the state.</param>
    /// <param name="reader">The reader, for the days and today's intervals.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The status.</returns>
    public static async Task<IntervalStatisticsStatus> ReadAsync(
        IntervalStatisticsRoller roller,
        IIntervalStatisticsReader reader,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roller);
        ArgumentNullException.ThrowIfNull(reader);

        var today = IntervalStatisticsConstants.DayStart(nowUtc);
        var state = await roller.GetStateAsync(cancellationToken);
        var days = await reader.GetDaysAsync(today.AddDays(-29), today.AddDays(1), cancellationToken);
        var todayIntervals = await reader.GetQueueIntervalsAsync(null, today, today.AddDays(1), cancellationToken);

        return Build(state, days, todayIntervals, nowUtc);
    }

    /// <summary>
    /// Builds the status from what was read.
    /// </summary>
    internal static IntervalStatisticsStatus Build(
        IntervalStatisticsState state,
        IReadOnlyList<IntervalStatisticsDay> days,
        IReadOnlyList<QueueIntervalStatistic> todayIntervals,
        DateTime nowUtc)
    {
        var today = IntervalStatisticsConstants.DayStart(nowUtc);
        var total = new QueueIntervalStatistic();

        foreach (var interval in todayIntervals)
        {
            total.Add(interval);
        }

        return new IntervalStatisticsStatus
        {
            State = state,
            DaysComputed = days.Count,
            DaysFinal = days.Count(day => day.Final),
            Today = days.FirstOrDefault(day => DateTime.SpecifyKind(day.Date, DateTimeKind.Utc) == today),
            TodayTotal = total,
            QueuesToday = todayIntervals.Select(interval => interval.QueueId).Distinct(StringComparer.Ordinal).Count(),
            Stalled = state.IsRebuilding && nowUtc - (state.LastProgressUtc > state.RebuildRequestedUtc ? state.LastProgressUtc : state.RebuildRequestedUtc) > StalledAfter,
            TodayUtc = today,
        };
    }
}
