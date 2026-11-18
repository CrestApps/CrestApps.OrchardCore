using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// Where the tickets created in a period came from and what they were: by channel, desk, ticket type, priority and
/// where they stand now.
/// </summary>
internal sealed class TicketDistributionReport : IReport
{
    private readonly TicketReportData _data;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public TicketDistributionReport(TicketReportData data, IContentDefinitionManager contentDefinitionManager, IStringLocalizer<TicketDistributionReport> stringLocalizer)
    {
        _data = data;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    public static string Name => "tickets-distribution";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketDistributionReport>("Ticket Distribution");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketDistributionReport>("The tickets created in the period by channel, desk, ticket type, priority and where they stand now.");

    public static string Category => ReportsConstants.Categories.Operations;

    public static Permission Permission => TicketsPermissions.ViewTicketReports;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _data.GetScopeAsync(context, cancellationToken) is not { } scope)
        {
            return TicketReportSections.NothingToShow(S);
        }

        var range = context.Filter.GetDateRange();
        var from = range.FromUtc ?? DateTime.MinValue;
        var to = range.ToUtc ?? DateTime.MaxValue;
        var tickets = await _data.ReadAsync(scope, index => index.CreatedUtc >= from && index.CreatedUtc <= to, index => index.CreatedUtc, cancellationToken);
        var names = await _data.GetNamesAsync(cancellationToken);
        var types = (await _contentDefinitionManager.ListTypeDefinitionsAsync()).ToDictionary(type => type.Name, type => type.DisplayName, StringComparer.Ordinal);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Created"].Value, ReportFormat.Number(tickets.Count)),
            ]));

        if (tickets.Count == 0)
        {
            var empty = ReportSection.ForTable(S["Tickets"].Value, [], []);
            empty.EmptyMessage = S["No tickets were created in these days."].Value;

            return document.Add(empty);
        }

        return document
            .Add(Bars(S["By channel"].Value, tickets, ticket => string.IsNullOrEmpty(ticket.Channel) ? S["Unknown"].Value : ticket.Channel))
            .Add(Bars(S["By desk"].Value, tickets, ticket => names.Desk(ticket.DeskId) ?? S["No desk"].Value))
            .Add(Bars(S["By ticket type"].Value, tickets, ticket => types.GetValueOrDefault(ticket.ContentType) ?? ticket.ContentType))
            .Add(Bars(S["By priority"].Value, tickets, ticket => names.Priority(ticket.PriorityId) ?? S["No priority"].Value))
            .Add(Bars(S["By where they stand now"].Value, tickets, ticket => StatusCategoryName(ticket.Category)));
    }

    private static ReportSection Bars(string title, List<TicketFacts> tickets, Func<TicketFacts, string> labelOf)
    {
        var groups = tickets.GroupBy(labelOf, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => (Label: group.Key, Count: group.Count()))
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var max = Math.Max(1, groups.Max(group => group.Count));

        return ReportSection.ForBars(title, groups.Select(group => new ReportBar(group.Label, ReportFormat.Number(group.Count), (double)group.Count / max)));
    }

    private string StatusCategoryName(TicketStatusCategory category)
        => category switch
        {
            TicketStatusCategory.New => S["New"].Value,
            TicketStatusCategory.Open => S["Open"].Value,
            TicketStatusCategory.Pending => S["Pending"].Value,
            TicketStatusCategory.OnHold => S["On hold"].Value,
            TicketStatusCategory.Solved => S["Solved"].Value,
            _ => S["Closed"].Value,
        };
}
