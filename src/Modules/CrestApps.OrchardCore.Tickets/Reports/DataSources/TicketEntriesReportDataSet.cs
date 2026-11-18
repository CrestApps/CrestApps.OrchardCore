using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Reports.DataSources;

/// <summary>
/// One row per reply or internal note on a ticket the reader may see.
/// </summary>
internal sealed class TicketEntriesReportDataSet : IndexedReportDataSet<TimelineEntry, TimelineEntryIndex>
{
    private static readonly string[] _kinds = [TicketsConstants.EntryKinds.Reply, TicketsConstants.EntryKinds.Note];

    private readonly TicketReportAccess _access;
    private TicketScopedQuery _scope;

    public TicketEntriesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        TicketReportAccess access,
        IStringLocalizer<TicketEntriesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                TicketReportDataSets.TicketEntries,
                stringLocalizer["Ticket replies and notes"],
                stringLocalizer["One row per public reply and internal note on a ticket you can see: who wrote it, when, through which channel, and its text."]),
            session,
            authorizationService,
            TimelineConstants.CollectionName)
    {
        _access = access;

        var S = stringLocalizer;
        var entry = S["Entry"].Value;
        var author = S["Author"].Value;

        AddField("EntryId", S["Entry ID"], DataFieldType.Text, record => record.ItemId, entry);
        AddField(TicketReportDataSets.TicketIdField, S["Ticket ID"], DataFieldType.Text, record => record.Subject.Id, entry).Identifier(TicketReportDataSets.Ticket());
        AddField(nameof(TimelineEntry.Kind), S["Kind"], DataFieldType.Text, record => record.Kind, entry).Description = S["Reply for a public reply, Note for an internal note."].Value;
        AddField("IsInternal", S["Internal"], DataFieldType.Boolean, record => record.Visibility == TimelineVisibility.Internal, entry);
        AddField(nameof(TimelineEntry.Channel), S["Channel"], DataFieldType.Text, record => record.Channel, entry);
        AddField(nameof(TimelineEntry.Body), S["Text"], DataFieldType.Text, record => record.RedactedUtc is null ? record.Body : null, entry).Description = S["Empty when the entry was redacted."].Value;
        AddField("AttachmentCount", S["Attachments"], DataFieldType.Integer, record => record.AttachmentIds?.Count ?? 0, entry);
        AddField("Redacted", S["Redacted"], DataFieldType.Boolean, record => record.RedactedUtc is not null, entry);
        AddField(nameof(TimelineEntry.CreatedUtc), S["Written"], DataFieldType.DateTime, record => record.CreatedUtc, entry);

        AddField("AuthorKind", S["Author kind"], DataFieldType.Text, record => record.Author.Kind, author).Description = S["User for someone who signs in, or the kind of party, such as Contact."].Value;
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
            [TicketReportDataSets.TicketIdField] = index => index.SubjectId,
        };

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => await _access.GetScopeAsync(context?.User, cancellationToken) is not null;

    protected override async IAsyncEnumerable<TimelineEntry> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _scope = await _access.GetScopeAsync(query?.Context?.User, cancellationToken);

        if (_scope is null)
        {
            yield break;
        }

        await foreach (var row in _access.KeepVisibleAsync(_scope, base.LoadAsync(query, cancellationToken), row => row.Subject.Id, cancellationToken))
        {
            yield return row;
        }
    }

    protected override Task<Func<IQueryIndex<TimelineEntryIndex>, IQueryIndex<TimelineEntryIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
        => Task.FromResult<Func<IQueryIndex<TimelineEntryIndex>, IQueryIndex<TimelineEntryIndex>>>(_scope is null
            ? null
            : rows => rows.Where(index => index.SubjectKind == TicketsConstants.RecordKind && index.Kind.IsIn(_kinds)));
}
