using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// Tickets created and solved each day, the open backlog now and how old it is, and the backlog at the start of each day
/// as the daily snapshot recorded it.
/// </summary>
internal sealed class TicketVolumeReport : IReport
{
    // The upper bounds, in days, of the age groups of open tickets.
    private static readonly double[] _ageDays = [1, 3, 7, 28, double.MaxValue];

    private readonly TicketReportData _data;

    internal readonly IStringLocalizer S;

    public TicketVolumeReport(TicketReportData data, IStringLocalizer<TicketVolumeReport> stringLocalizer)
    {
        _data = data;
        S = stringLocalizer;
    }

    public static string Name => "tickets-volume-backlog";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketVolumeReport>("Ticket Volume and Backlog");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketVolumeReport>("Tickets created and solved each day, the open backlog and its age, and the backlog at the start of each day.");

    public static string Category => ReportsConstants.Categories.Operations;

    public static Permission Permission => TicketsPermissions.ViewTicketReports;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.LastDays(30);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await _data.GetScopeAsync(cancellationToken) is not { } scope)
        {
            return TicketReportSections.NothingToShow(S);
        }

        var range = context.Filter.GetDateRange();
        var from = range.FromUtc ?? DateTime.MinValue;
        var to = range.ToUtc ?? DateTime.MaxValue;
        var zone = await _data.GetTimeZoneAsync();
        var now = _data.UtcNow;

        var created = await _data.ReadAsync(scope, index => index.CreatedUtc >= from && index.CreatedUtc <= to, index => index.CreatedUtc, cancellationToken);
        var solved = await _data.ReadAsync(scope, index => index.SolvedUtc != null && index.SolvedUtc >= from && index.SolvedUtc <= to, index => index.CreatedUtc, cancellationToken);
        var open = (await _data.ReadAsync(scope, index => index.StatusCategory != TicketStatusCategory.Solved && index.StatusCategory != TicketStatusCategory.Closed, index => index.CreatedUtc, cancellationToken))
            .Where(ticket => ticket.IsOpen)
            .ToList();

        var days = created.Select(ticket => TicketReportData.Day(ticket.CreatedUtc, zone))
            .Concat(solved.Select(ticket => TicketReportData.Day(ticket.SolvedUtc.Value, zone)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var createdByDay = created.GroupBy(ticket => TicketReportData.Day(ticket.CreatedUtc, zone)).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var solvedByDay = solved.GroupBy(ticket => TicketReportData.Day(ticket.SolvedUtc.Value, zone)).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Created"].Value, ReportFormat.Number(created.Count)),
                new ReportMetric(S["Solved"].Value, ReportFormat.Number(solved.Count)),
                new ReportMetric(S["Open now"].Value, ReportFormat.Number(open.Count)),
                new ReportMetric(S["Overdue now"].Value, ReportFormat.Number(open.Count(ticket => ticket.NextDueUtc is { } due && due < now)), S["Past their next service-level target."].Value),
                new ReportMetric(S["Unassigned now"].Value, ReportFormat.Number(open.Count(ticket => string.IsNullOrEmpty(ticket.AssigneeId)))),
            ]));

        if (days.Count > 0)
        {
            document.Add(ReportSection.ForChart(S["Created and solved by day"].Value, new ReportChart
            {
                Type = ReportChartType.Bar,
                Labels = days,
                Datasets =
                [
                    new ReportChartDataset(S["Created"].Value, days.Select(day => (double)createdByDay.GetValueOrDefault(day))),
                    new ReportChartDataset(S["Solved"].Value, days.Select(day => (double)solvedByDay.GetValueOrDefault(day))),
                ],
            }, width: 12));
        }

        string[] ageLabels = [S["Under a day"].Value, S["1 to 3 days"].Value, S["3 to 7 days"].Value, S["1 to 4 weeks"].Value, S["Over 4 weeks"].Value];
        var ages = new int[_ageDays.Length];

        foreach (var ticket in open)
        {
            var ageDays = (now - ticket.CreatedUtc).TotalDays;
            ages[Array.FindIndex(_ageDays, bound => ageDays < bound)]++;
        }

        var maxAge = Math.Max(1, ages.Max());
        document.Add(ReportSection.ForBars(S["Age of open tickets"].Value, ages.Select((count, index) => new ReportBar(ageLabels[index], ReportFormat.Number(count), (double)count / maxAge))));

        var backlog = await _data.ReadBacklogAsync(scope, range.FromUtc, range.ToUtc, cancellationToken);
        var backlogDays = backlog.GroupBy(row => row.Day, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).ToList();
        var backlogSection = ReportSection.ForTable(S["Backlog at the start of each day"].Value,
            [
                new ReportColumn(S["Day"].Value),
                new ReportColumn(S["Open"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Overdue"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Unassigned"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Average age"].Value, ReportColumnAlign.End),
            ],
            backlogDays.Select(group =>
            {
                var count = group.Sum(row => row.Group.Count);
                var hours = group.Sum(row => row.Group.TotalAgeHours);

                return new ReportRow(
                [
                    group.Key,
                    ReportFormat.Number(count),
                    ReportFormat.Number(group.Sum(row => row.Group.Overdue)),
                    ReportFormat.Number(group.Where(row => string.IsNullOrEmpty(row.Group.AssigneeId)).Sum(row => row.Group.Count)),
                    count == 0 ? "-" : ReportFormat.Duration(hours / count * 3600),
                ]);
            }));
        backlogSection.EmptyMessage = S["No backlog was recorded in these days. The backlog is recorded once a day for each desk."].Value;

        return document.Add(backlogSection);
    }
}
