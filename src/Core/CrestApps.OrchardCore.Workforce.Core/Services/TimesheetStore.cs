using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores timesheets.
/// </summary>
internal sealed class TimesheetStore : DocumentCatalog<Timesheet, TimesheetIndex>
{
    public TimesheetStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<Timesheet> FindForPeriodAsync(string workerId, string calendarId, DateOnly periodStart, CancellationToken cancellationToken = default)
    {
        var start = WorkforceDates.ToKey(periodStart);

        return Session.Query<Timesheet, TimesheetIndex>(index => index.WorkerId == workerId && index.PayPeriodCalendarId == calendarId && index.PeriodStart == start, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Timesheet> FindForDateAsync(string workerId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var key = WorkforceDates.ToKey(date);

        return Session.Query<Timesheet, TimesheetIndex>(index => index.WorkerId == workerId && index.PeriodStart <= key && index.PeriodEnd >= key, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Timesheet>> ListForPeriodAsync(string calendarId, DateOnly periodStart, TimesheetStatus status, CancellationToken cancellationToken = default)
    {
        var start = WorkforceDates.ToKey(periodStart);
        var items = await Session.Query<Timesheet, TimesheetIndex>(index => index.PayPeriodCalendarId == calendarId && index.PeriodStart == start && index.Status == status, collection: CollectionName)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<Timesheet> Items)> PageAsync(TimesheetQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<Timesheet, TimesheetIndex>(collection: CollectionName);

        if (query.TeamIds is not null)
        {
            if (query.TeamIds.Count == 0)
            {
                return (0, []);
            }

            search = search.Where(index => index.TeamId.IsIn(query.TeamIds));
        }

        if (!string.IsNullOrEmpty(query.WorkerId))
        {
            var workerId = query.WorkerId;

            search = search.Where(index => index.WorkerId == workerId);
        }

        if (query.Statuses is { Count: > 0 })
        {
            var statuses = query.Statuses;

            search = search.Where(index => index.Status.IsIn(statuses));
        }

        if (!string.IsNullOrEmpty(query.PayPeriodCalendarId))
        {
            var calendarId = query.PayPeriodCalendarId;

            search = search.Where(index => index.PayPeriodCalendarId == calendarId);
        }

        if (query.PeriodStart.HasValue)
        {
            var start = WorkforceDates.ToKey(query.PeriodStart.Value);

            search = search.Where(index => index.PeriodStart == start);
        }

        var count = await search.CountAsync(cancellationToken);
        var page = await search.OrderByDescending(index => index.PeriodStart).ThenBy(index => index.WorkerId).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
