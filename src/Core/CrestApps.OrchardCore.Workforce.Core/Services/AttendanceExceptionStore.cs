using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores attendance exceptions.
/// </summary>
internal sealed class AttendanceExceptionStore : DocumentCatalog<AttendanceException, AttendanceExceptionIndex>
{
    public AttendanceExceptionStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public async Task<IReadOnlyList<AttendanceException>> ListForWorkerAsync(string workerId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var fromKey = WorkforceDates.ToKey(from);
        var toKey = WorkforceDates.ToKey(to);
        var items = await Session.Query<AttendanceException, AttendanceExceptionIndex>(index => index.WorkerId == workerId && index.Date >= fromKey && index.Date <= toKey, collection: CollectionName)
            .ListAsync(cancellationToken);

        return items.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<AttendanceException> Items)> PageAsync(AttendanceQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var search = Session.Query<AttendanceException, AttendanceExceptionIndex>(collection: CollectionName);

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

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;

            search = search.Where(index => index.Status == status);
        }

        if (query.Kind.HasValue)
        {
            var kind = query.Kind.Value;

            search = search.Where(index => index.Kind == kind);
        }

        if (query.From.HasValue)
        {
            var from = WorkforceDates.ToKey(query.From.Value);

            search = search.Where(index => index.Date >= from);
        }

        if (query.To.HasValue)
        {
            var to = WorkforceDates.ToKey(query.To.Value);

            search = search.Where(index => index.Date <= to);
        }

        var count = await search.CountAsync(cancellationToken);
        var page = await search.OrderByDescending(index => index.Date).ThenByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }
}
