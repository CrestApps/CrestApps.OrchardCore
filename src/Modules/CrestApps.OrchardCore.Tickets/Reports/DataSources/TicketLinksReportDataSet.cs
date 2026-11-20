using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DataSources;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Reports.DataSources;

/// <summary>
/// One row per link of a ticket the reader may see: the record it points to and how they relate.
/// </summary>
internal sealed class TicketLinksReportDataSet : IndexedReportDataSet<ContentItem, TicketIndex, ReportChildRow<ContentItem, TicketLink>>
{
    private readonly TicketReportAccess _access;

    public TicketLinksReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        TicketReportAccess access,
        IStringLocalizer<TicketLinksReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                TicketReportDataSets.TicketLinks,
                stringLocalizer["Ticket links"],
                stringLocalizer["One row per link of a ticket you can see: the linked record's kind and id and how they relate, such as Related, Duplicate or Problem."]),
            session,
            authorizationService,
            collection: null)
    {
        _access = access;

        var S = stringLocalizer;
        var link = S["Link"].Value;

        AddField(TicketReportDataSets.TicketIdField, S["Ticket ID"], DataFieldType.Text, row => row.Parent.ContentItemId, link).Identifier(TicketReportDataSets.Ticket());
        AddField("TicketCreatedUtc", S["Ticket created"], DataFieldType.DateTime, row => row.Parent.CreatedUtc, link);
        AddField("Position", S["Link number"], DataFieldType.Integer, row => row.Position, link);
        AddField("RecordKind", S["Linked record kind"], DataFieldType.Text, row => row.Child.Record.Kind, link).Description = S["The kind of record linked, such as Ticket, Order or WorkOrder."].Value;
        AddField("RecordId", S["Linked record ID"], DataFieldType.Text, row => row.Child.Record.Id, link);
        AddField(nameof(TicketLink.Kind), S["Relation"], DataFieldType.Text, row => row.Child.Kind, link);

        CompleteDescription();
    }

    protected override bool CanAggregate => false;

    protected override (string Field, Expression<Func<TicketIndex, DateTime>> Column)? DateColumn => ("TicketCreatedUtc", index => index.CreatedUtc);

    protected override IReadOnlyDictionary<string, Expression<Func<TicketIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TicketIndex, string>>>(StringComparer.Ordinal)
        {
            [TicketReportDataSets.TicketIdField] = index => index.ContentItemId,
        };

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => await _access.GetScopeAsync(context?.User, cancellationToken) is not null;

    protected override IEnumerable<ReportChildRow<ContentItem, TicketLink>> GetRows(ContentItem document)
        => ReportChildRow<ContentItem, TicketLink>.For(document, (document.TryGet<TicketPart>(out var part) ? part.Links ?? [] : []).Where(link => !link.Record.IsEmpty));

    protected override async Task<Func<IQueryIndex<TicketIndex>, IQueryIndex<TicketIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        if (await _access.GetScopeAsync(context?.User, cancellationToken) is not { } scope)
        {
            return null;
        }

        var predicate = TicketScopeFilter.ToPredicate(scope);

        return rows => rows.Where(predicate);
    }
}
