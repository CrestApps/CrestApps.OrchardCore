using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.TimeTracker.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// For the tickets solved in a period, how long they took against how long people worked on them: resolution time
/// (calendar time from creation to solved), work time (how long anyone was working, time two people worked at once
/// counted once, including time logged before the ticket was opened) and effort (every person's time added up), the
/// people on each ticket and the touch ratio (the work between creation and solving as a share of resolution time); by desk, priority, ticket type and assignee, and the tickets that took the most effort.
/// </summary>
internal sealed class TicketResolutionWorkReport : IReport
{
    // How many tickets the "most effort" table lists.
    private const int TopTickets = 25;

    private readonly TicketReportData _data;
    private readonly TicketTimeReportData _time;

    internal readonly IStringLocalizer S;

    public TicketResolutionWorkReport(TicketReportData data, TicketTimeReportData time, IStringLocalizer<TicketResolutionWorkReport> stringLocalizer)
    {
        _data = data;
        _time = time;
        S = stringLocalizer;
    }

    public static string Name => "tickets-resolution-work-time";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketResolutionWorkReport>("Ticket Resolution and Work Time");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketResolutionWorkReport>("For the tickets solved in the period: how long they took against the time people worked on them, the people on each ticket and the touch ratio, by desk, priority, ticket type and assignee.");

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
        var entries = solved.Count == 0
            ? null
            : await _time.ReadEntriesAsync([.. solved.Select(ticket => ticket.Id)], cancellationToken);
        var now = _data.UtcNow;
        var tickets = solved.Select(ticket => Measure(ticket, entries?[ticket.Id], now)).ToList();
        var logged = tickets.Where(ticket => ticket.People > 0).ToList();

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Solved"].Value, ReportFormat.Number(tickets.Count)),
                new ReportMetric(S["Solved with time logged"].Value, TicketReportData.Rate(logged.Count, tickets.Count), S["Share of the solved tickets someone logged time on. The other measures count only these."].Value),
                new ReportMetric(S["Median resolution time"].Value, Time(TicketReportData.Median(logged.Select(ticket => ticket.ResolutionMinutes))), S["Calendar time from the ticket's creation to when it was solved."].Value),
                new ReportMetric(S["Median work time"].Value, Time(TicketReportData.Median(logged.Select(ticket => (double?)ticket.WorkMinutes))), S["How long anyone was working on the ticket; time two people worked at once counts once."].Value),
                new ReportMetric(S["Median effort"].Value, Time(TicketReportData.Median(logged.Select(ticket => (double?)ticket.EffortMinutes))), S["Every person's time on the ticket added up."].Value),
                new ReportMetric(S["People per ticket"].Value, Average(logged.Select(ticket => (double)ticket.People))),
                new ReportMetric(S["Touch ratio"].Value, TouchRatio(logged), S["Work time as a share of resolution time, over all the tickets: how much of their life was spent working on them."].Value),
            ]));

        if (logged.Count == 0)
        {
            var empty = ReportSection.ForTable(S["Tickets"].Value, [], []);
            empty.EmptyMessage = tickets.Count == 0
                ? S["No tickets were solved in these days."].Value
                : S["No time was logged on the tickets solved in these days."].Value;

            return document.Add(empty);
        }

        var names = await _data.GetNamesAsync(cancellationToken);
        var types = await _time.GetTypeNamesAsync();
        var assignees = await _data.GetUserNamesAsync(logged.Select(ticket => ticket.Ticket.AssigneeId), cancellationToken);

        document
            .Add(Table(S["By desk"].Value, S["Desk"].Value, logged, ticket => ticket.DeskId, id => names.Desk(id) ?? S["No desk"].Value))
            .Add(Table(S["By priority"].Value, S["Priority"].Value, logged, ticket => ticket.PriorityId, id => names.Priority(id) ?? S["No priority"].Value))
            .Add(Table(S["By ticket type"].Value, S["Ticket type"].Value, logged, ticket => ticket.ContentType, id => types.GetValueOrDefault(id) ?? id))
            .Add(Table(S["By assignee"].Value, S["Assignee"].Value, logged, ticket => ticket.AssigneeId, id => string.IsNullOrEmpty(id) ? S["Unassigned"].Value : assignees.GetValueOrDefault(id) ?? id));

        var top = logged
            .OrderByDescending(ticket => ticket.EffortMinutes)
            .ThenBy(ticket => ticket.Ticket.Number, StringComparer.Ordinal)
            .Take(TopTickets)
            .Select(ticket => new ReportRow(
            [
                ticket.Ticket.Number ?? ticket.Ticket.Id,
                Time(ticket.ResolutionMinutes),
                Time(ticket.WorkMinutes),
                Time(ticket.EffortMinutes),
                ReportFormat.Number(ticket.People),
                ticket.ResolutionMinutes is > 0 ? ReportFormat.Percent(ticket.LifeWorkMinutes / ticket.ResolutionMinutes.Value) : "-",
            ]));

        return document.Add(ReportSection.ForTable(S["Tickets that took the most effort"].Value,
            [
                new ReportColumn(S["Ticket"].Value),
                new ReportColumn(S["Resolution time"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Work time"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Effort"].Value, ReportColumnAlign.End),
                new ReportColumn(S["People"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Touch ratio"].Value, ReportColumnAlign.End),
            ],
            top));
    }

    private static SolvedTicket Measure(TicketFacts ticket, IEnumerable<TimeEntry> entries, DateTime now)
    {
        var spans = (entries ?? []).Select(entry => (Start: entry.StartUtc, End: entry.EndUtc ?? now, entry.UserId)).Where(span => span.End > span.Start).ToList();

        // The touch ratio is about the ticket's life, so it counts only the work between its creation and its solving.
        var solvedUtc = ticket.SolvedUtc ?? now;
        var duringLife = spans.Select(span => (Start: span.Start < ticket.CreatedUtc ? ticket.CreatedUtc : span.Start, End: span.End > solvedUtc ? solvedUtc : span.End));

        return new SolvedTicket(
            ticket,
            ticket.ResolutionMinutes,
            TicketTimeReportData.WorkMinutes(spans.Select(span => (span.Start, span.End))),
            spans.Sum(span => (span.End - span.Start).TotalMinutes),
            spans.Select(span => span.UserId).Distinct(StringComparer.Ordinal).Count(),
            TicketTimeReportData.WorkMinutes(duringLife));
    }

    private ReportSection Table(string title, string label, List<SolvedTicket> tickets, Func<TicketFacts, string> keyOf, Func<string, string> nameOf)
    {
        var rows = tickets
            .GroupBy(ticket => keyOf(ticket.Ticket) ?? string.Empty, StringComparer.Ordinal)
            .Select(group => (Name: nameOf(group.Key), Tickets: group.ToList()))
            .OrderByDescending(row => row.Tickets.Count)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new ReportRow(
            [
                row.Name,
                ReportFormat.Number(row.Tickets.Count),
                Time(TicketReportData.Median(row.Tickets.Select(ticket => ticket.ResolutionMinutes))),
                Time(TicketReportData.Median(row.Tickets.Select(ticket => (double?)ticket.WorkMinutes))),
                Time(TicketReportData.Median(row.Tickets.Select(ticket => (double?)ticket.EffortMinutes))),
                Time(row.Tickets.Sum(ticket => ticket.EffortMinutes)),
                TouchRatio(row.Tickets),
            ]));

        return ReportSection.ForTable(title,
            [
                new ReportColumn(label),
                new ReportColumn(S["Solved"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median resolution time"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median work time"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median effort"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Effort in all"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Touch ratio"].Value, ReportColumnAlign.End),
            ],
            rows);
    }

    private static string TouchRatio(IEnumerable<SolvedTicket> tickets)
    {
        var measured = tickets.Where(ticket => ticket.ResolutionMinutes is > 0).ToList();
        var resolution = measured.Sum(ticket => ticket.ResolutionMinutes.Value);

        return resolution <= 0 ? "-" : ReportFormat.Percent(measured.Sum(ticket => ticket.LifeWorkMinutes) / resolution);
    }

    private static string Average(IEnumerable<double> values)
    {
        var known = values.ToList();

        return known.Count == 0 ? "-" : Math.Round(known.Average(), 1).ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string Time(double? minutes)
        => minutes is { } value ? ReportFormat.Duration(Math.Round(value) * 60) : "-";

    private sealed record SolvedTicket(TicketFacts Ticket, double? ResolutionMinutes, double WorkMinutes, double EffortMinutes, int People, double LifeWorkMinutes);
}
