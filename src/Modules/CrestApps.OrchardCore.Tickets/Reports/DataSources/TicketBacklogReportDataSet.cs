using System.Globalization;
using System.Linq.Expressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Reports.DataSources;

/// <summary>
/// The open tickets of each desk at the start of each day, as the daily snapshot recorded them: one row per day, desk,
/// status, priority, team, assignee, ticket type and channel. A reader who sees only their teams' or their own tickets
/// gets only those rows.
/// </summary>
internal sealed class TicketBacklogReportDataSet : IndexedReportDataSet<TicketBacklogDay, TicketBacklogIndex, ReportChildRow<TicketBacklogDay, TicketBacklogGroup>>
{
    private readonly TicketReportAccess _access;
    private TicketScopedQuery _scope;
    private TicketReportNames _names;

    public TicketBacklogReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        TicketReportAccess access,
        IStringLocalizer<TicketBacklogReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                TicketReportDataSets.TicketBacklog,
                stringLocalizer["Ticket backlog"],
                stringLocalizer["The open tickets at the start of each day, as recorded that day: how many, how many were overdue and how old they were, by desk, status, priority, team and assignee."]),
            session,
            authorizationService,
            collection: null)
    {
        _access = access;

        var S = stringLocalizer;
        var day = S["Day"].Value;
        var group = S["Tickets"].Value;

        AddField(nameof(TicketBacklogDay.Day), S["Day"], DataFieldType.Date, row => DateTime.ParseExact(row.Parent.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture), day).Description = S["The day, in the site's time zone."].Value;
        AddField(nameof(TicketBacklogDay.TakenUtc), S["Recorded"], DataFieldType.DateTime, row => row.Parent.TakenUtc, day);
        AddField(nameof(TicketBacklogDay.DeskId), S["Desk ID"], DataFieldType.Text, row => row.Parent.DeskId, day);
        AddField("Desk", S["Desk"], DataFieldType.Text, row => _names?.Desk(row.Parent.DeskId), day);

        AddField(nameof(TicketBacklogGroup.StatusId), S["Status ID"], DataFieldType.Text, row => row.Child.StatusId, group);
        AddField("Status", S["Status"], DataFieldType.Text, row => _names?.Status(row.Child.StatusId), group);
        AddField(nameof(TicketBacklogGroup.StatusCategory), S["Status category"], DataFieldType.Text, row => row.Child.StatusCategory.ToString(), group);
        AddField(nameof(TicketBacklogGroup.PriorityId), S["Priority ID"], DataFieldType.Text, row => row.Child.PriorityId, group);
        AddField("Priority", S["Priority"], DataFieldType.Text, row => _names?.Priority(row.Child.PriorityId), group);
        AddField(nameof(TicketBacklogGroup.TeamId), S["Team ID"], DataFieldType.Text, row => row.Child.TeamId, group);
        AddField(nameof(TicketBacklogGroup.AssigneeId), S["Assignee"], DataFieldType.Text, row => row.Child.AssigneeId, group).Identifier(ReportDataReferences.User());
        AddField("TicketType", S["Ticket type"], DataFieldType.Text, row => row.Child.ContentType, group);
        AddField(nameof(TicketBacklogGroup.Channel), S["Channel"], DataFieldType.Text, row => row.Child.Channel, group);
        AddField(nameof(TicketBacklogGroup.CategoryId), S["Category ID"], DataFieldType.Text, row => row.Child.CategoryId, group).Identifier(TicketReportDataSets.Category()).Description = S["Empty on days recorded before the backlog was broken down by category."].Value;
        AddField("Category", S["Category"], DataFieldType.Text, row => _names?.Category(row.Child.CategoryId)?.Name ?? row.Child.CategoryId, group);
        AddField("CategoryPathText", S["Category path"], DataFieldType.Text, row => _names?.Category(row.Child.CategoryId)?.PathText, group);
        AddField(nameof(TicketBacklogGroup.Count), S["Open tickets"], DataFieldType.Integer, row => row.Child.Count, group);
        AddField(nameof(TicketBacklogGroup.Overdue), S["Overdue"], DataFieldType.Integer, row => row.Child.Overdue, group).Description = S["How many were past their next service-level target."].Value;
        AddField("AverageAgeHours", S["Average age (hours)"], DataFieldType.Decimal, row => row.Child.Count > 0 ? Math.Round(row.Child.TotalAgeHours / row.Child.Count, 1) : 0, group);
        AddField(nameof(TicketBacklogGroup.OldestCreatedUtc), S["Oldest created"], DataFieldType.DateTime, row => row.Child.OldestCreatedUtc, group);

        CompleteDescription();
    }

    protected override bool CanAggregate => false;

    protected override (string Field, Expression<Func<TicketBacklogIndex, DateTime>> Column)? DateColumn => (nameof(TicketBacklogDay.TakenUtc), index => index.TakenUtc);

    protected override IReadOnlyDictionary<string, Expression<Func<TicketBacklogIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TicketBacklogIndex, string>>>(StringComparer.Ordinal)
        {
            [nameof(TicketBacklogDay.DeskId)] = index => index.DeskId,
        };

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => await _access.GetScopeAsync(context?.User, cancellationToken) is not null;

    protected override IEnumerable<ReportChildRow<TicketBacklogDay, TicketBacklogGroup>> GetRows(TicketBacklogDay document)
        => ReportChildRow<TicketBacklogDay, TicketBacklogGroup>.For(
            document,
            document.Groups.Where(group => TicketScopeFilter.Allows(_scope, document.DeskId, group.TeamId, group.AssigneeId)));

    protected override async Task<Func<IQueryIndex<TicketBacklogIndex>, IQueryIndex<TicketBacklogIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        _scope = await _access.GetScopeAsync(context?.User, cancellationToken);

        if (_scope is null)
        {
            return null;
        }

        _names = await _access.GetNamesAsync(cancellationToken);
        var desks = _scope.AllDeskIds.Concat(_scope.TeamDeskIds).Concat(_scope.AssignedDeskIds).Distinct(StringComparer.Ordinal).ToArray();

        return rows => rows.Where(index => index.DeskId.IsIn(desks));
    }
}
