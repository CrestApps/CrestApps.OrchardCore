using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Dispatch.Services;

/// <summary>
/// What the Visits page filters its list by.
/// </summary>
internal sealed class VisitListFilter
{
    /// <summary>
    /// Gets the status: <see cref="VisitListQuery.OpenFilter"/>, <see cref="VisitListQuery.AllFilter"/> or a
    /// <see cref="VisitStatus"/> name.
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Gets the text the visit number, the work order number or the summary contains, or <see langword="null"/>.
    /// </summary>
    public string Search { get; init; }

    /// <summary>
    /// Gets the day the visits overlap, in <see cref="TimeZoneId"/>, or <see langword="null"/> for any day.
    /// </summary>
    public DateOnly? Day { get; init; }

    /// <summary>
    /// Gets the time zone the day is in.
    /// </summary>
    public string TimeZoneId { get; init; }

    /// <summary>
    /// Gets the technician the visits are given to, or <see langword="null"/>.
    /// </summary>
    public string WorkerId { get; init; }
}

/// <summary>
/// The query of the Visits page: by status (or every open one), a search on the visit number, work order number or
/// summary, a day and a technician; most urgent first, then by start, then newest.
/// </summary>
internal static class VisitListQuery
{
    /// <summary>
    /// The status filter for visits still to happen or happening.
    /// </summary>
    public const string OpenFilter = "open";

    /// <summary>
    /// The status filter for every visit.
    /// </summary>
    public const string AllFilter = "all";

    /// <summary>
    /// Gives a page of the visits that match a filter and how many match.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="filter">The filter.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to give at most.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many match, and the page.</returns>
    public static async Task<(int Count, IReadOnlyList<ServiceVisit> Items)> PageAsync(ISession session, VisitListFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(filter);

        var query = session.Query<ServiceVisit, ServiceVisitIndex>(collection: DispatchConstants.CollectionName);

        if (filter.Status == OpenFilter)
        {
            query = query.Where(index => index.IsOpen);
        }
        else if (Enum.TryParse<VisitStatus>(filter.Status, out var status))
        {
            var value = status.ToString();

            query = query.Where(index => index.Status == value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var q = filter.Search.Trim();

            query = query.Where(index => index.Number.Contains(q) || index.WorkOrderNumber.Contains(q) || index.Summary.Contains(q));
        }

        if (filter.Day is { } day)
        {
            var from = CalendarTime.ToUtc(day.ToDateTime(TimeOnly.MinValue), filter.TimeZoneId);
            var to = CalendarTime.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), filter.TimeZoneId);

            query = query.Where(index => index.StartUtc < to && index.EndUtc > from);
        }

        if (!string.IsNullOrEmpty(filter.WorkerId))
        {
            var workerId = filter.WorkerId;
            var ids = (await session.QueryIndex<VisitWorkerIndex>(index => index.WorkerId == workerId, collection: DispatchConstants.CollectionName).ListAsync(cancellationToken))
                .Select(index => index.VisitId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            query = query.Where(index => index.ItemId.IsIn(ids));
        }

        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.Priority).ThenBy(index => index.StartUtc).ThenByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToList());
    }
}
