using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Which running service-level timers to find: past their target, past their warning point, or due soon.
/// </summary>
internal enum ServiceLevelDueStatus
{
    /// <summary>
    /// Past their warning point or their target, or due within the window.
    /// </summary>
    Any,

    /// <summary>
    /// Past their target.
    /// </summary>
    Breached,

    /// <summary>
    /// Past their warning point and not yet due.
    /// </summary>
    AtRisk,
}

/// <summary>
/// Pages through the running service-level timers that are due, the most overdue first, on the same index the
/// Service-level timers report reads. A paused timer is never listed: its due time is frozen while the work waits.
/// </summary>
internal sealed class ServiceLevelDueTimers
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelDueTimers"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ServiceLevelDueTimers(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Lists a page of the due timers.
    /// </summary>
    /// <param name="filter">What to list.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>The timers, the earliest due first.</returns>
    public async Task<IReadOnlyList<ServiceLevelTimer>> ListAsync(ServiceLevelDueFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var timers = await Query(filter)
            .OrderBy(index => index.DueUtc)
            .ThenBy(index => index.Id)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Max(take, 1))
            .ListAsync(cancellationToken);

        return [.. timers];
    }

    /// <summary>
    /// Counts the due timers.
    /// </summary>
    /// <param name="filter">What to count.</param>
    /// <param name="cancellationToken">A token to cancel the query.</param>
    /// <returns>How many there are.</returns>
    public Task<int> CountAsync(ServiceLevelDueFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return Query(filter).CountAsync(cancellationToken);
    }

    private IQuery<ServiceLevelTimer, ServiceLevelTimerIndex> Query(ServiceLevelDueFilter filter)
    {
        var now = filter.NowUtc;
        var dueBy = filter.DueWithin is { } within && within > TimeSpan.Zero ? now.Add(within) : now;

        var query = filter.Status switch
        {
            ServiceLevelDueStatus.Breached => _session.Query<ServiceLevelTimer, ServiceLevelTimerIndex>(
                index => index.State == ServiceLevelTimerState.Running && index.DueUtc <= now,
                collection: ServiceLevelsConstants.CollectionName),
            ServiceLevelDueStatus.AtRisk => _session.Query<ServiceLevelTimer, ServiceLevelTimerIndex>(
                index => index.State == ServiceLevelTimerState.Running && index.WarnAtUtc <= now && index.DueUtc > now,
                collection: ServiceLevelsConstants.CollectionName),
            _ => _session.Query<ServiceLevelTimer, ServiceLevelTimerIndex>(
                index => index.State == ServiceLevelTimerState.Running && (index.WarnAtUtc <= now || index.DueUtc <= dueBy),
                collection: ServiceLevelsConstants.CollectionName),
        };

        if (!string.IsNullOrEmpty(filter.SubjectKind))
        {
            var kind = filter.SubjectKind;
            query = query.Where(index => index.SubjectKind == kind);
        }

        if (!string.IsNullOrEmpty(filter.Metric))
        {
            var metric = filter.Metric;
            query = query.Where(index => index.Metric == metric);
        }

        return query;
    }
}

/// <summary>
/// What <see cref="ServiceLevelDueTimers"/> lists.
/// </summary>
internal sealed class ServiceLevelDueFilter
{
    /// <summary>
    /// Gets or sets the current instant, in UTC.
    /// </summary>
    public DateTime NowUtc { get; set; }

    /// <summary>
    /// Gets or sets which timers to list.
    /// </summary>
    public ServiceLevelDueStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a window: with <see cref="ServiceLevelDueStatus.Any"/>, the timers due within it are listed too.
    /// </summary>
    public TimeSpan? DueWithin { get; set; }

    /// <summary>
    /// Gets or sets the kind of record to list, or <see langword="null"/> for every kind.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the metric to list, or <see langword="null"/> for every metric.
    /// </summary>
    public string Metric { get; set; }
}
