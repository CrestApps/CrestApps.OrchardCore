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
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Reports.DataSources;

/// <summary>
/// One row per change made to a ticket the reader may see: the field, its value before and after, who and when. This is
/// the ticket's history as it was recorded, so status, priority, team and assignee changes can be counted over time.
/// </summary>
internal sealed class TicketChangesReportDataSet : IndexedReportDataSet<TimelineEntry, TimelineEntryIndex, ReportChildRow<TimelineEntry, TimelineFieldChange>>
{
    private readonly TicketReportAccess _access;
    private TicketScopedQuery _scope;

    public TicketChangesReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        TicketReportAccess access,
        IStringLocalizer<TicketChangesReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                TicketReportDataSets.TicketChanges,
                stringLocalizer["Ticket changes"],
                stringLocalizer["One row per change to a ticket you can see: the field, its value before and after, who made it and when."]),
            session,
            authorizationService,
            TimelineConstants.CollectionName)
    {
        _access = access;

        var S = stringLocalizer;
        var change = S["Change"].Value;

        AddField("EntryId", S["Entry ID"], DataFieldType.Text, row => row.Parent.ItemId, change);
        AddField(TicketReportDataSets.TicketIdField, S["Ticket ID"], DataFieldType.Text, row => row.Parent.Subject.Id, change).Identifier(TicketReportDataSets.Ticket());
        AddField(nameof(TimelineFieldChange.Field), S["Field"], DataFieldType.Text, row => row.Child.Field, change).Description = S["What changed, such as Status, Priority, Team, Assignee or Desk."].Value;
        AddField(nameof(TimelineFieldChange.From), S["From"], DataFieldType.Text, row => row.Child.From, change);
        AddField(nameof(TimelineFieldChange.To), S["To"], DataFieldType.Text, row => row.Child.To, change);
        AddField("ChangedUtc", S["Changed"], DataFieldType.DateTime, row => row.Parent.CreatedUtc, change);
        AddField(nameof(TimelineEntry.Kind), S["Entry kind"], DataFieldType.Text, row => row.Parent.Kind, change).Description = S["Event for a change on its own, or Reply or Note for a change made with a reply or note."].Value;
        AddField("AuthorKind", S["Changed by kind"], DataFieldType.Text, row => row.Parent.Author.Kind, change);
        AddField("AuthorUserId", S["Changed by user ID"], DataFieldType.Text, row => row.Parent.Author.Kind == PartyKinds.User ? row.Parent.Author.Id : null, change).Identifier(ReportDataReferences.User());
        AddField(nameof(TimelineEntry.AuthorName), S["Changed by"], DataFieldType.Text, row => row.Parent.AuthorName, change);

        CompleteDescription();
    }

    protected override bool CanAggregate => false;

    protected override (string Field, Expression<Func<TimelineEntryIndex, DateTime>> Column)? DateColumn => ("ChangedUtc", index => index.CreatedUtc);

    protected override IReadOnlyDictionary<string, Expression<Func<TimelineEntryIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TimelineEntryIndex, string>>>(StringComparer.Ordinal)
        {
            [TicketReportDataSets.TicketIdField] = index => index.SubjectId,
        };

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => await _access.GetScopeAsync(context?.User, cancellationToken) is not null;

    protected override IEnumerable<ReportChildRow<TimelineEntry, TimelineFieldChange>> GetRows(TimelineEntry document)
        => ReportChildRow<TimelineEntry, TimelineFieldChange>.For(document, document.Changes);

    protected override async IAsyncEnumerable<ReportChildRow<TimelineEntry, TimelineFieldChange>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _scope = await _access.GetScopeAsync(query?.Context?.User, cancellationToken);

        if (_scope is null)
        {
            yield break;
        }

        await foreach (var row in _access.KeepVisibleAsync(_scope, base.LoadAsync(query, cancellationToken), row => row.Parent.Subject.Id, cancellationToken))
        {
            yield return row;
        }
    }

    protected override Task<Func<IQueryIndex<TimelineEntryIndex>, IQueryIndex<TimelineEntryIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
        => Task.FromResult<Func<IQueryIndex<TimelineEntryIndex>, IQueryIndex<TimelineEntryIndex>>>(_scope is null
            ? null
            : rows => rows.Where(index => index.SubjectKind == TicketsConstants.RecordKind));
}
