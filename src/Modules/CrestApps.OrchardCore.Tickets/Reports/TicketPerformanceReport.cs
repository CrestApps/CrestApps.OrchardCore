using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// For each assignee and each team, the tickets solved in a period, how long they took, and how often they were reopened
/// or escalated, with each one's open tickets now.
/// </summary>
internal sealed class TicketPerformanceReport : IReport
{
    private readonly TicketReportData _data;

    internal readonly IStringLocalizer S;

    public TicketPerformanceReport(TicketReportData data, IStringLocalizer<TicketPerformanceReport> stringLocalizer)
    {
        _data = data;
        S = stringLocalizer;
    }

    public static string Name => "tickets-agent-team-performance";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketPerformanceReport>("Ticket Agent and Team Performance");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketPerformanceReport>("For each assignee and team: tickets solved in the period, the time they took, reopen and escalation rates, and open tickets now.");

    public static string Category => ReportsConstants.Categories.AgentPerformance;

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
        var solved = await _data.ReadAsync(scope, index => index.SolvedUtc != null && index.SolvedUtc >= from && index.SolvedUtc <= to, index => index.CreatedUtc, cancellationToken);
        var open = (await _data.ReadAsync(scope, index => index.StatusCategory != TicketStatusCategory.Solved && index.StatusCategory != TicketStatusCategory.Closed, index => index.CreatedUtc, cancellationToken))
            .Where(ticket => ticket.IsOpen)
            .ToList();

        var users = await _data.GetUserNamesAsync(solved.Select(ticket => ticket.AssigneeId).Concat(open.Select(ticket => ticket.AssigneeId)), cancellationToken);
        var teams = await _data.GetTeamNamesAsync(cancellationToken);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Solved"].Value, ReportFormat.Number(solved.Count)),
                new ReportMetric(S["Reopened"].Value, TicketReportData.Rate(solved.Count(ticket => ticket.ReopenCount > 0), solved.Count), S["Share of the solved tickets that were reopened at least once."].Value),
                new ReportMetric(S["Escalated"].Value, TicketReportData.Rate(solved.Count(ticket => ticket.EscalationLevel > 0), solved.Count), S["Share of the solved tickets that were escalated."].Value),
                new ReportMetric(S["Solved on first contact"].Value, TicketReportData.Rate(solved.Count(ticket => ticket.SolvedOnFirstContact), solved.Count), S["Solved with exactly one public reply from an agent (not counting automated replies) and never reopened."].Value),
            ]));

        document.Add(Table(
            S["By assignee"].Value,
            S["Assignee"].Value,
            solved,
            open,
            ticket => ticket.AssigneeId,
            id => string.IsNullOrEmpty(id) ? S["Unassigned"].Value : users.GetValueOrDefault(id) ?? id));

        document.Add(Table(
            S["By team"].Value,
            S["Team"].Value,
            solved,
            open,
            ticket => ticket.TeamId,
            id => string.IsNullOrEmpty(id) ? S["No team"].Value : teams.GetValueOrDefault(id) ?? id));

        return document;
    }

    private ReportSection Table(string title, string label, List<TicketFacts> solved, List<TicketFacts> open, Func<TicketFacts, string> keyOf, Func<string, string> nameOf)
    {
        var keys = solved.Select(keyOf).Concat(open.Select(keyOf)).Select(key => key ?? string.Empty).Distinct(StringComparer.Ordinal);
        var solvedBy = solved.ToLookup(ticket => keyOf(ticket) ?? string.Empty, StringComparer.Ordinal);
        var openBy = open.ToLookup(ticket => keyOf(ticket) ?? string.Empty, StringComparer.Ordinal);

        var rows = keys
            .Select(key => (Name: nameOf(key), Solved: solvedBy[key].ToList(), Open: openBy[key].Count()))
            .OrderByDescending(row => row.Solved.Count)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new ReportRow(
            [
                row.Name,
                ReportFormat.Number(row.Solved.Count),
                TicketReportData.Minutes(TicketReportData.Median(row.Solved.Select(ticket => ticket.ResolutionMinutes))),
                TicketReportData.Minutes(TicketReportData.Median(row.Solved.Select(ticket => ticket.FirstResponseMinutes))),
                TicketReportData.Rate(row.Solved.Count(ticket => ticket.ReopenCount > 0), row.Solved.Count),
                TicketReportData.Rate(row.Solved.Count(ticket => ticket.EscalationLevel > 0), row.Solved.Count),
                ReportFormat.Number(row.Open),
            ]))
            .ToList();

        var section = ReportSection.ForTable(title,
            [
                new ReportColumn(label),
                new ReportColumn(S["Solved"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median time to solve (calendar time)"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median first response"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Reopened"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Escalated"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Open now"].Value, ReportColumnAlign.End),
            ],
            rows);
        section.EmptyMessage = S["No tickets were solved in these days, and none is open."].Value;

        return section;
    }
}
