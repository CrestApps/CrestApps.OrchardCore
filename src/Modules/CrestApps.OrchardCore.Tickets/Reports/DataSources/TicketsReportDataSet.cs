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
/// One row per ticket the reader may see, as it is now: its desk, status, priority, people, time stamps and counters.
/// </summary>
internal sealed class TicketsReportDataSet : IndexedReportDataSet<ContentItem, TicketIndex>
{
    private readonly TicketReportAccess _access;
    private TicketReportNames _names;
    private ContentItem _lastItem;
    private TicketPart _lastPart;

    public TicketsReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        TicketReportAccess access,
        IStringLocalizer<TicketsReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                TicketReportDataSets.Tickets,
                stringLocalizer["Tickets"],
                stringLocalizer["One row per ticket you can see, as it is now: desk, status, priority, requester, team, assignee, its times and counts."]),
            session,
            authorizationService,
            collection: null)
    {
        _access = access;

        var S = stringLocalizer;
        var ticket = S["Ticket"].Value;
        var people = S["People"].Value;
        var dates = S["Dates"].Value;
        var counts = S["Counts"].Value;

        AddField(TicketReportDataSets.TicketIdField, S["Ticket ID"], DataFieldType.Text, record => record.ContentItemId, ticket)
            .Identifier(ReportDataReferences.ContentItem(TicketsConstants.DefaultContentType))
            .Description = S["The ticket's content item id. Custom fields of the Ticket type are in the Contents data source under the same id."].Value;
        AddField(nameof(TicketPart.Number), S["Number"], DataFieldType.Text, record => Part(record).Number, ticket);
        AddField("Subject", S["Subject"], DataFieldType.Text, record => record.DisplayText, ticket);
        AddField("TicketType", S["Ticket type"], DataFieldType.Text, record => record.ContentType, ticket).Description = S["The content type of the ticket."].Value;
        AddField(nameof(TicketPart.DeskId), S["Desk ID"], DataFieldType.Text, record => Part(record).DeskId, ticket);
        AddField("Desk", S["Desk"], DataFieldType.Text, record => _names?.Desk(Part(record).DeskId), ticket);
        AddField(nameof(TicketPart.StatusId), S["Status ID"], DataFieldType.Text, record => Part(record).StatusId, ticket);
        AddField("Status", S["Status"], DataFieldType.Text, record => _names?.Status(Part(record).StatusId), ticket);
        AddField(nameof(TicketPart.StatusCategory), S["Status category"], DataFieldType.Text, record => Part(record).StatusCategory.ToString(), ticket).Description = S["New, Open, Pending, OnHold, Solved or Closed."].Value;
        AddField(nameof(TicketPart.PriorityId), S["Priority ID"], DataFieldType.Text, record => Part(record).PriorityId, ticket);
        AddField("Priority", S["Priority"], DataFieldType.Text, record => _names?.Priority(Part(record).PriorityId), ticket);
        AddField(nameof(TicketPart.CategoryId), S["Category ID"], DataFieldType.Text, record => Part(record).CategoryId, ticket).Identifier(TicketReportDataSets.Category());
        AddField("Category", S["Category"], DataFieldType.Text, record => _names?.Category(Part(record).CategoryId)?.Name ?? Part(record).CategoryId, ticket).Description = S["The category's own name, such as Laptop."].Value;
        AddField("CategoryPathText", S["Category path"], DataFieldType.Text, record => _names?.Category(Part(record).CategoryId)?.PathText, ticket).Description = S["The category with those above it, such as Hardware > Laptop."].Value;
        AddField("CategoryLevel1", S["Top category"], DataFieldType.Text, record => CategoryAt(Part(record).CategoryPath, 0), ticket).Description = S["The top category, to group by, such as Hardware."].Value;
        AddField("CategoryLevel2", S["Second-level category"], DataFieldType.Text, record => CategoryAt(Part(record).CategoryPath, 1), ticket);
        AddField(nameof(TicketPart.Channel), S["Channel"], DataFieldType.Text, record => Part(record).Channel, ticket).Description = S["Where the ticket came from, such as Agent, Portal, Api or Email."].Value;
        AddField(nameof(TicketPart.MergedIntoId), S["Merged into"], DataFieldType.Text, record => Part(record).MergedIntoId, ticket).Identifier(TicketReportDataSets.Ticket());
        AddField(nameof(TicketPart.FollowUpOfId), S["Follow-up of"], DataFieldType.Text, record => Part(record).FollowUpOfId, ticket).Identifier(TicketReportDataSets.Ticket());

        AddField("Requester", S["Requester"], DataFieldType.Text, record => Part(record).Requester.IsEmpty ? null : Part(record).Requester.ToString(), people).Description = S["The requester as its kind and id, such as a user or a contact."].Value;
        AddField("RequesterName", S["Requester name"], DataFieldType.Text, record => Part(record).RequesterContact?.Name, people);
        AddField("RequesterEmail", S["Requester e-mail"], DataFieldType.Text, record => Part(record).RequesterContact?.Email, people);
        AddField(nameof(TicketPart.AccountId), S["Account ID"], DataFieldType.Text, record => Part(record).AccountId, people);
        AddField(nameof(TicketPart.TeamId), S["Team ID"], DataFieldType.Text, record => Part(record).TeamId, people);
        AddField(nameof(TicketPart.AssigneeId), S["Assignee"], DataFieldType.Text, record => Part(record).AssigneeId, people).Identifier(ReportDataReferences.User());

        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, dates);
        AddField("ModifiedUtc", S["Last changed"], DataFieldType.DateTime, record => record.ModifiedUtc, dates);
        AddField(nameof(TicketPart.FirstResponseUtc), S["First response"], DataFieldType.DateTime, record => Part(record).FirstResponseUtc, dates);
        AddField(nameof(TicketPart.AssignedUtc), S["Assigned"], DataFieldType.DateTime, record => Part(record).AssignedUtc, dates);
        AddField(nameof(TicketPart.SolvedUtc), S["Solved"], DataFieldType.DateTime, record => Part(record).SolvedUtc, dates);
        AddField(nameof(TicketPart.ClosedUtc), S["Closed"], DataFieldType.DateTime, record => Part(record).ClosedUtc, dates);
        AddField(nameof(TicketPart.NextDueUtc), S["Next due"], DataFieldType.DateTime, record => Part(record).NextDueUtc, dates).Description = S["When the next service-level target is due; empty when none is running."].Value;
        AddField(nameof(TicketPart.LastCustomerActivityUtc), S["Last customer activity"], DataFieldType.DateTime, record => Part(record).LastCustomerActivityUtc, dates);
        AddField(nameof(TicketPart.LastAgentActivityUtc), S["Last agent activity"], DataFieldType.DateTime, record => Part(record).LastAgentActivityUtc, dates);

        AddField("FirstResponseMinutes", S["Minutes to first response"], DataFieldType.Decimal, record => Minutes(record.CreatedUtc, Part(record).FirstResponseUtc), counts).Description = S["Calendar minutes from creation to the first public reply from an agent."].Value;
        AddField("ResolutionMinutes", S["Minutes to solve"], DataFieldType.Decimal, record => Minutes(record.CreatedUtc, Part(record).SolvedUtc), counts).Description = S["Calendar minutes from creation to the latest time it was solved."].Value;
        AddField(nameof(TicketPart.ReplyCount), S["Replies"], DataFieldType.Integer, record => Part(record).ReplyCount, counts);
        AddField(nameof(TicketPart.ReopenCount), S["Reopened"], DataFieldType.Integer, record => Part(record).ReopenCount, counts);
        AddField(nameof(TicketPart.ReassignmentCount), S["Reassigned"], DataFieldType.Integer, record => Part(record).ReassignmentCount, counts);
        AddField(nameof(TicketPart.EscalationLevel), S["Escalation level"], DataFieldType.Integer, record => Part(record).EscalationLevel, counts);
        AddField("SatisfactionScore", S["Rating"], DataFieldType.Integer, record => Part(record).Satisfaction?.Score, counts).Description = S["The requester's rating from 1 to 5, once given."].Value;
        AddField("SatisfactionComment", S["Rating comment"], DataFieldType.Text, record => Part(record).Satisfaction?.Comment, counts);
        AddField("LinkCount", S["Links"], DataFieldType.Integer, record => Part(record).Links?.Count ?? 0, counts);
        AddField("ParticipantCount", S["Participants"], DataFieldType.Integer, record => Part(record).Participants?.Count ?? 0, counts);

        CompleteDescription();
    }

    protected override bool CanAggregate => false;

    protected override (string Field, Expression<Func<TicketIndex, DateTime>> Column)? DateColumn => ("CreatedUtc", index => index.CreatedUtc);

    protected override IReadOnlyDictionary<string, Expression<Func<TicketIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<TicketIndex, string>>>(StringComparer.Ordinal)
        {
            [TicketReportDataSets.TicketIdField] = index => index.ContentItemId,
            [nameof(TicketPart.Number)] = index => index.Number,
        };

    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => await _access.GetScopeAsync(context?.User, cancellationToken) is not null;

    protected override async Task<Func<IQueryIndex<TicketIndex>, IQueryIndex<TicketIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        if (await _access.GetScopeAsync(context?.User, cancellationToken) is not { } scope)
        {
            return null;
        }

        _names = await _access.GetNamesAsync(cancellationToken);
        var predicate = TicketScopeFilter.ToPredicate(scope);

        return rows => rows.Where(predicate);
    }

    // The name of the category at a depth of a ticket's category path (0 at the top), or none.
    private string CategoryAt(string path, int level)
    {
        var ids = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

        return level < ids.Length ? _names?.Category(ids[level])?.Name ?? ids[level] : null;
    }

    // Each field reads the part; it is read from the item once per row.
    private TicketPart Part(ContentItem item)
    {
        if (!ReferenceEquals(item, _lastItem))
        {
            _lastPart = item.TryGet<TicketPart>(out var part) ? part : new TicketPart();
            _lastItem = item;
        }

        return _lastPart;
    }

    internal static double? Minutes(DateTime? from, DateTime? to)
        => from is { } start && to is { } end && end >= start ? Math.Round((end - start).TotalMinutes, 1) : null;
}
