using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// Reads the interval statistics through their day indexes.
/// </summary>
public sealed class IntervalStatisticsReader : IIntervalStatisticsReader
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalStatisticsReader"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public IntervalStatisticsReader(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<QueueIntervalStatistic>> GetQueueIntervalsAsync(IEnumerable<string> queueIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var (firstDay, lastDay) = Days(fromUtc, toUtc);
        var ids = queueIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids is { Length: 0 })
        {
            return [];
        }

        var query = _session.Query<QueueIntervalStatistics, QueueIntervalStatisticsIndex>(
            index => index.Date >= firstDay && index.Date <= lastDay,
            collection: ContactCenterStorage.CollectionName);

        if (ids is not null)
        {
            query = query.Where(index => index.QueueId.IsIn(ids));
        }

        var days = await query.ListAsync(cancellationToken);

        return [.. days
            .SelectMany(day => day.Intervals)
            .Where(interval => interval.IntervalStartUtc >= fromUtc && interval.IntervalStartUtc < toUtc)
            .OrderBy(interval => interval.IntervalStartUtc)
            .ThenBy(interval => interval.QueueId, StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgentIntervalStatistic>> GetAgentIntervalsAsync(IEnumerable<string> userIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var (firstDay, lastDay) = Days(fromUtc, toUtc);
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids is { Length: 0 })
        {
            return [];
        }

        var query = _session.Query<AgentIntervalStatistics, AgentIntervalStatisticsIndex>(
            index => index.Date >= firstDay && index.Date <= lastDay,
            collection: ContactCenterStorage.CollectionName);

        if (ids is not null)
        {
            query = query.Where(index => index.UserId.IsIn(ids));
        }

        var days = await query.ListAsync(cancellationToken);

        return [.. days
            .SelectMany(day => day.Intervals)
            .Where(interval => interval.IntervalStartUtc >= fromUtc && interval.IntervalStartUtc < toUtc)
            .OrderBy(interval => interval.IntervalStartUtc)
            .ThenBy(interval => interval.AgentId, StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IntervalStatisticsDay>> GetDaysAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var (firstDay, lastDay) = Days(fromUtc, toUtc);
        var version = IntervalStatisticsConstants.Version;

        var days = await _session.Query<IntervalStatisticsDay, IntervalStatisticsDayIndex>(
                index => index.Date >= firstDay && index.Date <= lastDay && index.Version == version,
                collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return [.. days.OrderBy(day => day.Date)];
    }

    private static (DateTime First, DateTime Last) Days(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc)
        {
            throw new ArgumentException("The end of the range must be after its start.", nameof(toUtc));
        }

        return (IntervalStatisticsConstants.DayStart(fromUtc), IntervalStatisticsConstants.DayStart(toUtc.AddTicks(-1)));
    }
}
