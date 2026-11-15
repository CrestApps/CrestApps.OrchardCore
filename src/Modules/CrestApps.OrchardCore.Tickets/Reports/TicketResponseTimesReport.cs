using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// How long tickets created in a period waited for a first response and took to be solved, overall and by priority,
/// and how many were solved on first contact.
/// </summary>
internal sealed class TicketResponseTimesReport : IReport
{
    private readonly TicketReportData _data;

    internal readonly IStringLocalizer S;

    public TicketResponseTimesReport(TicketReportData data, IStringLocalizer<TicketResponseTimesReport> stringLocalizer)
    {
        _data = data;
        S = stringLocalizer;
    }

    public static string Name => "tickets-response-times";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketResponseTimesReport>("Ticket Response and Resolution Times");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketResponseTimesReport>("How long the tickets created in the period waited for a first response and took to solve, by priority, and how many were solved on first contact.");

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
        var responded = tickets.Count(ticket => ticket.FirstResponseMinutes is not null);
        var solved = tickets.Where(ticket => ticket.SolvedUtc is not null).ToList();

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Created"].Value, ReportFormat.Number(tickets.Count)),
                new ReportMetric(S["Responded"].Value, TicketReportData.Rate(responded, tickets.Count), S["Share of the tickets that got a first response from an agent."].Value),
                new ReportMetric(S["Median first response (calendar time)"].Value, TicketReportData.Minutes(TicketReportData.Median(tickets.Select(ticket => ticket.FirstResponseMinutes)))),
                new ReportMetric(S["Median time to solve (calendar time)"].Value, TicketReportData.Minutes(TicketReportData.Median(solved.Select(ticket => ticket.ResolutionMinutes)))),
                new ReportMetric(S["Solved on first contact"].Value, TicketReportData.Rate(solved.Count(ticket => ticket.SolvedOnFirstContact), solved.Count), S["Share of the solved tickets solved with exactly one public reply from an agent (not counting automated replies) and never reopened."].Value),
            ]));

        var byPriority = tickets
            .GroupBy(ticket => ticket.PriorityId ?? string.Empty, StringComparer.Ordinal)
            .OrderBy(group => names.Priority(group.Key) ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new ReportRow(
            [
                names.Priority(group.Key) ?? S["No priority"].Value,
                ReportFormat.Number(group.Count()),
                TicketReportData.Minutes(TicketReportData.Median(group.Select(ticket => ticket.FirstResponseMinutes))),
                TicketReportData.Minutes(TicketReportData.Average(group.Select(ticket => ticket.FirstResponseMinutes))),
                TicketReportData.Minutes(TicketReportData.Median(group.Select(ticket => ticket.ResolutionMinutes))),
                TicketReportData.Minutes(TicketReportData.Average(group.Select(ticket => ticket.ResolutionMinutes))),
            ]))
            .ToList();

        var table = ReportSection.ForTable(S["By priority"].Value,
            [
                new ReportColumn(S["Priority"].Value),
                new ReportColumn(S["Created"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median first response"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average first response"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median time to solve"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average time to solve"].Value, ReportColumnAlign.End),
            ],
            byPriority);
        table.Description = S["Times are calendar time from when the ticket was created. Service-level targets, counted in business hours, are in the service level report."].Value;
        table.EmptyMessage = S["No tickets were created in these days."].Value;

        return document.Add(table);
    }
}
