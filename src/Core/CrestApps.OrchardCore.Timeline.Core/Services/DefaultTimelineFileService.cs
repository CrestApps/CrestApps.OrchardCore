using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Timeline.Core.Services;

/// <summary>
/// Lists the files of records' timelines from <see cref="TimelineAttachmentIndex"/>, newest first, never those of
/// redacted entries.
/// </summary>
public sealed class DefaultTimelineFileService : ITimelineFileService
{
    // A record is listed with at most this many others (a ticket and the tickets merged into it).
    private const int MaxSubjects = 100;

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTimelineFileService"/> class.
    /// </summary>
    public DefaultTimelineFileService(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<TimelineFilePage> ListAsync(TimelineFileQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var subjects = (query.Subjects ?? []).Where(subject => !subject.IsEmpty).Distinct().Take(MaxSubjects).ToList();

        if (subjects.Count == 0 || query.Take <= 0)
        {
            return new TimelineFilePage();
        }

        // The subjects are mostly of one kind (a ticket and its merged tickets); the kind is checked again below.
        var kinds = subjects.Select(subject => subject.Kind).Distinct(StringComparer.Ordinal).ToArray();
        var ids = subjects.Select(subject => subject.Id).Distinct(StringComparer.Ordinal).ToArray();
        var keys = subjects.Select(subject => subject.ToString()).ToHashSet(StringComparer.Ordinal);
        var search = _session.QueryIndex<TimelineAttachmentIndex>(
            index => index.SubjectKind.IsIn(kinds) && index.SubjectId.IsIn(ids) && !index.Redacted,
            collection: TimelineConstants.CollectionName);

        // Internal entries' files only of the records whose internal entries the reader may read.
        var internalIds = (query.InternalSubjects ?? [])
            .Where(subject => keys.Contains(subject.ToString()))
            .Select(subject => subject.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        search = internalIds.Length == 0
            ? search.Where(index => index.Visibility == TimelineVisibility.Public)
            : search.Where(index => index.Visibility == TimelineVisibility.Public || index.SubjectId.IsIn(internalIds));

        var total = kinds.Length == 1 ? await search.CountAsync(cancellationToken) : -1;
        var rows = (await search
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .Skip(Math.Max(0, query.Skip))
            .Take(query.Take)
            .ListAsync(cancellationToken))
            .Where(row => keys.Contains(row.SubjectKind + ":" + row.SubjectId))
            .ToList();

        return new TimelineFilePage
        {
            Total = total >= 0 ? total : rows.Count + Math.Max(0, query.Skip),
            Files = [.. rows.Select(row => new TimelineFile
            {
                AttachmentId = row.AttachmentId,
                EntryId = row.EntryId,
                Subject = new RecordRef(row.SubjectKind, row.SubjectId),
                Kind = row.Kind,
                Visibility = row.Visibility,
                AuthorName = row.AuthorName,
                CreatedUtc = row.CreatedUtc,
            })],
        };
    }
}
