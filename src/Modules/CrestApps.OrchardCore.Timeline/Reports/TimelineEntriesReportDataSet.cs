using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Timeline.Reports;

/// <summary>
/// The Timeline data source: comments, internal notes and events on every kind of record.
/// </summary>
internal sealed class TimelineReportDataSource : RecordDataSource, IDataSource
{
    public const string SourceName = "Timeline";

    public const string EntriesDataSet = "TimelineEntries";

    private readonly IRecordDataSet[] _dataSets;

    public TimelineReportDataSource(TimelineEntriesReportDataSet entries)
    {
        _dataSets = [entries];
    }

    public static string Name => SourceName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TimelineReportDataSource>("Timeline");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TimelineReportDataSource>("Comments, internal notes and events on the records you can see, such as tickets, work orders, orders and accounts.");

    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}

/// <summary>
/// One row per timeline entry the reader may see. Each record kind decides who sees its entries, through its timeline
/// access provider; an entry of a kind with no provider is never shown.
/// </summary>
internal sealed class TimelineEntriesReportDataSet : IndexedReportDataSet<TimelineEntry, TimelineEntryIndex>
{
    private readonly ITimelineAccessService _access;

    public TimelineEntriesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        ITimelineAccessService access,
        IStringLocalizer<TimelineEntriesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                TimelineReportDataSource.EntriesDataSet,
                stringLocalizer["Timeline entries"],
                stringLocalizer["One row per comment, internal note or event on a record you can see: the record, who wrote it, when, and its text."]),
            session,
            authorizationService,
            TimelineConstants.CollectionName)
    {
        _access = access;

        var S = stringLocalizer;
        var entry = S["Entry"].Value;
        var author = S["Author"].Value;

        AddField("EntryId", S["Entry ID"], DataFieldType.Text, record => record.ItemId, entry);
        AddField("SubjectKind", S["Record kind"], DataFieldType.Text, record => record.Subject.Kind, entry).Description = S["The kind of record the entry is on, such as Ticket, WorkOrder, Order or Content."].Value;
        AddField("SubjectId", S["Record ID"], DataFieldType.Text, record => record.Subject.Id, entry);
        AddField(nameof(TimelineEntry.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, entry).Description = S["Comment, Note or Event, or a kind a module adds, such as Reply."].Value;
        AddField("IsInternal", S["Internal"], DataFieldType.Boolean, record => record.Visibility == TimelineVisibility.Internal, entry);
        AddField(nameof(TimelineEntry.Channel), S["Channel"], DataFieldType.Text, record => record.Channel, entry);
        AddField(nameof(TimelineEntry.Body), S["Text"], DataFieldType.Text, record => record.RedactedUtc is null ? record.Body : null, entry).Description = S["Empty when the entry was redacted."].Value;
        AddField("ChangeCount", S["Changes"], DataFieldType.Integer, record => record.Changes?.Count ?? 0, entry).Description = S["How many field changes the entry records."].Value;
        AddField("AttachmentCount", S["Attachments"], DataFieldType.Integer, record => record.AttachmentIds?.Count ?? 0, entry);
        AddField("Redacted", S["Redacted"], DataFieldType.Boolean, record => record.RedactedUtc is not null, entry);
        AddField(nameof(TimelineEntry.CreatedUtc), S["Written"], DataFieldType.DateTime, record => record.CreatedUtc, entry);

        AddField("AuthorKind", S["Author kind"], DataFieldType.Text, record => record.Author.Kind, author);
        AddField("AuthorId", S["Author ID"], DataFieldType.Text, record => record.Author.Id, author);
        AddField("AuthorUserId", S["Author user ID"], DataFieldType.Text, record => record.Author.Kind == PartyKinds.User ? record.Author.Id : null, author).Identifier(ReportDataReferences.User());
        AddField(nameof(TimelineEntry.AuthorName), S["Author name"], DataFieldType.Text, record => record.AuthorName, author);

        CompleteDescription();
    }

    protected override bool CanAggregate => false;

    protected override (string Field, Expression<Func<TimelineEntryIndex, DateTime>> Column)? DateColumn => (nameof(TimelineEntry.CreatedUtc), index => index.CreatedUtc);

    protected override IReadOnlyDictionary<string, Expression<Func<TimelineEntryIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TimelineEntryIndex, string>>>(StringComparer.Ordinal)
        {
            ["SubjectId"] = index => index.SubjectId,
        };

    protected override async IAsyncEnumerable<TimelineEntry> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var user = query?.Context?.User;

        if (user is null)
        {
            yield break;
        }

        // Who may see a record's entries is asked for a buffer of rows at a time, one question per record kind and
        // visibility, so a year of entries on many records costs a query per buffer, not one per record.
        var buffer = new List<TimelineEntry>(AccessBatchSize);

        await foreach (var entry in base.LoadAsync(query, cancellationToken))
        {
            buffer.Add(entry);

            if (buffer.Count == AccessBatchSize)
            {
                foreach (var visible in await VisibleAsync(user, buffer, cancellationToken))
                {
                    yield return visible;
                }

                buffer.Clear();
            }
        }

        foreach (var visible in await VisibleAsync(user, buffer, cancellationToken))
        {
            yield return visible;
        }
    }

    private async Task<List<TimelineEntry>> VisibleAsync(System.Security.Claims.ClaimsPrincipal user, List<TimelineEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var allowed = new Dictionary<TimelineVisibility, IReadOnlySet<RecordRef>>();

        foreach (var visibility in entries.Select(entry => entry.Visibility).Distinct())
        {
            allowed[visibility] = await _access.GetVisibleAsync(user, [.. entries.Where(entry => entry.Visibility == visibility).Select(entry => entry.Subject).Distinct()], visibility, cancellationToken);
        }

        return [.. entries.Where(entry => allowed[entry.Visibility].Contains(entry.Subject))];
    }

    // Rows checked at a time.
    private const int AccessBatchSize = 500;
}
