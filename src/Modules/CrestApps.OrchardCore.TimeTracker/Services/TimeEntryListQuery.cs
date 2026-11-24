using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Models;
using YesSql;

namespace CrestApps.OrchardCore.TimeTracker.Services;

/// <summary>
/// The query of the Time Entries list: a search in the person's name, a stage and a time range, used by the list page
/// and the <c>listTimeEntries</c> AI tool.
/// </summary>
internal static class TimeEntryListQuery
{
    /// <summary>
    /// Builds the query; the caller orders and pages it.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="search">Text in the person's name, or <see langword="null"/>.</param>
    /// <param name="stageId">The stage, or <see langword="null"/>.</param>
    /// <param name="fromUtc">The start of the range; with <paramref name="toUtc"/>, only entries that ended after it.</param>
    /// <param name="toUtc">The end of the range; with <paramref name="fromUtc"/>, only entries that started before it.</param>
    /// <param name="userId">The person, or <see langword="null"/> for everyone.</param>
    /// <returns>The query.</returns>
    public static IQuery<TimeEntry, TimeEntryIndex> Build(ISession session, string search, string stageId, DateTime? fromUtc, DateTime? toUtc, string userId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var query = session.Query<TimeEntry, TimeEntryIndex>(collection: TimeTrackerConstants.CollectionName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();

            query = query.Where(index => index.UserName.Contains(q));
        }

        if (!string.IsNullOrEmpty(stageId))
        {
            query = query.Where(index => index.StageId == stageId);
        }

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(index => index.UserId == userId);
        }

        if (fromUtc is { } from && toUtc is { } to)
        {
            query = query.Where(index => index.StartUtc < to && index.EndUtc > from);
        }

        return query;
    }
}
