using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// The time logged on tickets in a period, from the timer and from time logged after the fact: in all and billable, by
/// person, account, desk and ticket type, and the tickets with the most time. Each entry counts only its part inside
/// the period, and only the tickets the reader may see count.
/// </summary>
internal sealed class TicketTimeReport : IReport
{
    // How many tickets the "most time" table lists.
    private const int TopTickets = 25;

    private readonly TicketReportData _data;
    private readonly TicketTimeReportData _time;

    internal readonly IStringLocalizer S;

    public TicketTimeReport(TicketReportData data, TicketTimeReportData time, IStringLocalizer<TicketTimeReport> stringLocalizer)
    {
        _data = data;
        _time = time;
        S = stringLocalizer;
    }

    public static string Name => "tickets-time";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketTimeReport>("Ticket Time");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketTimeReport>("Time logged on tickets in the period: in all and billable, by person, account, desk and ticket type, and the tickets with the most time.");

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
        var work = await _time.ReadWorkAsync(scope, range.FromUtc, range.ToUtc, cancellationToken);
        var total = work.Sum(item => item.Minutes);
        var billable = work.Where(item => item.Billable).Sum(item => item.Minutes);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Time on tickets"].Value, Time(total)),
                new ReportMetric(S["Billable"].Value, Time(billable), S["Time in the desks' ticket stage, billed to the customer."].Value),
                new ReportMetric(S["Not billed"].Value, Time(total - billable), S["Time in a desk's stage for work not billed to the customer."].Value),
                new ReportMetric(S["Tickets worked on"].Value, ReportFormat.Number(work.Select(item => item.Ticket.Id).Distinct(StringComparer.Ordinal).Count())),
                new ReportMetric(S["People"].Value, ReportFormat.Number(work.Select(item => item.Entry.UserId).Distinct(StringComparer.Ordinal).Count())),
            ]));

        if (work.Count == 0)
        {
            var empty = ReportSection.ForTable(S["Time"].Value, [], []);
            empty.EmptyMessage = S["No time was logged on tickets in these days."].Value;

            return document.Add(empty);
        }

        var people = work
            .GroupBy(item => item.Entry.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Entry.UserName).LastOrDefault(name => !string.IsNullOrEmpty(name)) ?? group.Key, StringComparer.Ordinal);
        var accounts = await _data.GetAccountNamesAsync(work.Select(item => item.Ticket.AccountId), cancellationToken);
        var names = await _data.GetNamesAsync(cancellationToken);
        var types = await _time.GetTypeNamesAsync();

        document
            .Add(Table(S["By person"].Value, S["Person"].Value, work, item => item.Entry.UserId, id => people.GetValueOrDefault(id) ?? id))
            .Add(Table(S["By account"].Value, S["Account"].Value, work, item => item.Ticket.AccountId, id => string.IsNullOrEmpty(id) ? S["No account"].Value : accounts.GetValueOrDefault(id) ?? id))
            .Add(Table(S["By desk"].Value, S["Desk"].Value, work, item => item.Ticket.DeskId, id => names.Desk(id) ?? S["No desk"].Value))
            .Add(Table(S["By ticket type"].Value, S["Ticket type"].Value, work, item => item.Ticket.ContentType, id => types.GetValueOrDefault(id) ?? id));

        var top = work
            .GroupBy(item => item.Ticket.Id, StringComparer.Ordinal)
            .Select(group => (Ticket: group.First().Ticket, Minutes: group.Sum(item => item.Minutes), Billable: group.Where(item => item.Billable).Sum(item => item.Minutes), People: group.Select(item => item.Entry.UserId).Distinct(StringComparer.Ordinal).Count()))
            .OrderByDescending(row => row.Minutes)
            .ThenBy(row => row.Ticket.Number, StringComparer.Ordinal)
            .Take(TopTickets)
            .Select(row => new ReportRow(
            [
                row.Ticket.Number ?? row.Ticket.Id,
                string.IsNullOrEmpty(row.Ticket.AccountId) ? string.Empty : accounts.GetValueOrDefault(row.Ticket.AccountId) ?? row.Ticket.AccountId,
                names.Desk(row.Ticket.DeskId) ?? string.Empty,
                Time(row.Minutes),
                Time(row.Billable),
                ReportFormat.Number(row.People),
            ]));

        return document.Add(ReportSection.ForTable(S["Tickets with the most time"].Value,
            [
                new ReportColumn(S["Ticket"].Value),
                new ReportColumn(S["Account"].Value),
                new ReportColumn(S["Desk"].Value),
                new ReportColumn(S["Time"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Billable"].Value, ReportColumnAlign.End),
                new ReportColumn(S["People"].Value, ReportColumnAlign.End),
            ],
            top));
    }

    private ReportSection Table(string title, string label, List<TicketWork> work, Func<TicketWork, string> keyOf, Func<string, string> nameOf)
    {
        var rows = work
            .GroupBy(item => keyOf(item) ?? string.Empty, StringComparer.Ordinal)
            .Select(group =>
            {
                var minutes = group.Sum(item => item.Minutes);
                var tickets = group.Select(item => item.Ticket.Id).Distinct(StringComparer.Ordinal).Count();

                return (Name: nameOf(group.Key), Minutes: minutes, Billable: group.Where(item => item.Billable).Sum(item => item.Minutes), Tickets: tickets);
            })
            .OrderByDescending(row => row.Minutes)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new ReportRow(
            [
                row.Name,
                Time(row.Minutes),
                Time(row.Billable),
                row.Minutes <= 0 ? "-" : ReportFormat.Percent(row.Billable / row.Minutes),
                ReportFormat.Number(row.Tickets),
                Time(row.Minutes / row.Tickets),
            ]));

        return ReportSection.ForTable(title,
            [
                new ReportColumn(label),
                new ReportColumn(S["Time"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Billable"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Billable share"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Tickets"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Time per ticket"].Value, ReportColumnAlign.End),
            ],
            rows);
    }

    private static string Time(double minutes)
        => ReportFormat.Duration(Math.Round(minutes) * 60);
}
