using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Timeline.Core.Services;

/// <summary>
/// Stores timeline entries.
/// </summary>
public sealed class TimelineEntryStore : DocumentCatalog<TimelineEntry, TimelineEntryIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineEntryStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public TimelineEntryStore(ISession session)
        : base(session)
    {
        CollectionName = TimelineConstants.CollectionName;
    }

    /// <summary>
    /// Gets a page of a record's entries, newest first.
    /// </summary>
    /// <param name="query">The record and the entries to include.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entries and how many match in all.</returns>
    public async Task<(IReadOnlyList<TimelineEntry> Entries, int Total)> PageAsync(TimelineQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Subject.IsEmpty)
        {
            return ([], 0);
        }

        var kind = query.Subject.Kind;
        var id = query.Subject.Id;
        var kinds = query.Kinds?.Where(value => !string.IsNullOrEmpty(value)).ToArray() ?? [];
        var includeInternal = query.IncludeInternal;

        var entries = Session.Query<TimelineEntry, TimelineEntryIndex>(index => index.SubjectKind == kind && index.SubjectId == id, collection: CollectionName);

        if (!includeInternal)
        {
            entries = entries.Where(index => index.Visibility == TimelineVisibility.Public);
        }

        if (kinds.Length > 0)
        {
            entries = entries.Where(index => index.Kind.IsIn(kinds));
        }

        var total = await entries.CountAsync(cancellationToken);
        var page = await entries
            .OrderByDescending(index => index.CreatedUtc)
            .Skip(Math.Max(query.Skip, 0))
            .Take(Math.Clamp(query.Take, 1, 500))
            .ListAsync(cancellationToken);

        return (page.ToArray(), total);
    }

    /// <summary>
    /// Lists every entry of a record, oldest first.
    /// </summary>
    /// <param name="subject">The record.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The entries.</returns>
    public async Task<IReadOnlyList<TimelineEntry>> ListBySubjectAsync(RecordRef subject, CancellationToken cancellationToken = default)
    {
        if (subject.IsEmpty)
        {
            return [];
        }

        var kind = subject.Kind;
        var id = subject.Id;
        var entries = await Session.Query<TimelineEntry, TimelineEntryIndex>(index => index.SubjectKind == kind && index.SubjectId == id, collection: CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ListAsync(cancellationToken);

        return entries.ToArray();
    }
}
