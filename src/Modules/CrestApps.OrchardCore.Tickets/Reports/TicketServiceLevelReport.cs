using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Reports.DataSources;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// How the service-level targets of the reader's tickets went: for each metric and for each account, the targets that
/// ended in the period, how many were met and breached, and the ones running now that are already breached.
/// </summary>
internal sealed class TicketServiceLevelReport : IReport
{
    private readonly TicketReportData _data;
    private readonly IEnumerable<IServiceLevelScopeProvider> _scopes;

    internal readonly IStringLocalizer S;

    public TicketServiceLevelReport(
        TicketReportData data,
        IEnumerable<IServiceLevelScopeProvider> scopes,
        IStringLocalizer<TicketServiceLevelReport> stringLocalizer)
    {
        _data = data;
        _scopes = scopes;
        S = stringLocalizer;
    }

    public static string Name => "tickets-service-levels";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketServiceLevelReport>("Ticket Service Level Compliance");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketServiceLevelReport>("For each target, such as first response or resolution, how many ended in the period, how many were met or breached, and how many running now are breached.");

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
        const string kind = TicketsConstants.RecordKind;

        var ended = await ReadAsync(scope, rows => rows.Where(index => index.SubjectKind == kind && index.EndedUtc != null && index.EndedUtc >= from && index.EndedUtc <= to), cancellationToken);
        var running = await ReadAsync(scope, rows => rows.Where(index => index.SubjectKind == kind && index.IsActive), cancellationToken);
        var metricNames = await GetMetricNamesAsync(cancellationToken);
        var met = ended.Count(timer => !timer.IsBreached);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Targets ended"].Value, ReportFormat.Number(ended.Count)),
                new ReportMetric(S["Met"].Value, TicketReportData.Rate(met, ended.Count)),
                new ReportMetric(S["Breached"].Value, ReportFormat.Number(ended.Count - met)),
                new ReportMetric(S["Running now, breached"].Value, ReportFormat.Number(running.Count(timer => timer.IsBreached))),
                new ReportMetric(S["Running now, warned"].Value, ReportFormat.Number(running.Count(timer => !timer.IsBreached && timer.WarnedUtc is not null))),
            ]));

        var rows = ended.Concat(running)
            .GroupBy(timer => timer.Metric ?? string.Empty, StringComparer.Ordinal)
            .OrderBy(group => metricNames.GetValueOrDefault(group.Key) ?? group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group =>
            {
                var done = group.Where(timer => timer.EndedUtc is { } end && end >= from && end <= to).ToList();
                var doneMet = done.Count(timer => !timer.IsBreached);
                var used = done.Where(timer => timer.ElapsedSeconds is not null).Select(timer => (double?)timer.ElapsedSeconds.Value / 60);

                return new ReportRow(
                [
                    metricNames.GetValueOrDefault(group.Key) ?? group.Key,
                    ReportFormat.Number(done.Count),
                    ReportFormat.Number(doneMet),
                    ReportFormat.Number(done.Count - doneMet),
                    TicketReportData.Rate(doneMet, done.Count),
                    TicketReportData.Minutes(TicketReportData.Median(used)),
                    ReportFormat.Number(group.Count(timer => timer.IsActive && timer.IsBreached)),
                ]);
            })
            .ToList();

        var table = ReportSection.ForTable(S["By target"].Value,
            [
                new ReportColumn(S["Target"].Value),
                new ReportColumn(S["Ended"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Met"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Breached"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Compliance"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Median business time used"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Running, breached"].Value, ReportColumnAlign.End),
            ],
            rows);
        table.Description = S["A target counts in the period it ended. Business time is counted on the desk's business hours."].Value;
        table.EmptyMessage = S["No service-level targets ended in these days."].Value;
        document.Add(table);

        return document.Add(await ByAccountAsync(scope, ended, running, from, to, cancellationToken));
    }

    // Each account's targets: the ones that ended in the period, met or breached, and the ones running late now.
    private async Task<ReportSection> ByAccountAsync(TicketScopedQuery scope, List<ServiceLevelTimer> ended, List<ServiceLevelTimer> running, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var timers = ended.Concat(running).DistinctBy(timer => timer.ItemId, StringComparer.Ordinal).ToList();
        var tickets = await _data.ReadByIdsAsync(scope, timers.Select(timer => timer.Subject.Id), cancellationToken);
        var accounts = await _data.GetAccountNamesAsync(tickets.Values.Select(ticket => ticket.AccountId), cancellationToken);
        string AccountOf(ServiceLevelTimer timer) => tickets.TryGetValue(timer.Subject.Id ?? string.Empty, out var ticket) ? ticket.AccountId ?? string.Empty : string.Empty;

        var rows = timers
            .GroupBy(AccountOf, StringComparer.Ordinal)
            .Select(group =>
            {
                var done = group.Where(timer => timer.EndedUtc is { } end && end >= from && end <= to).ToList();
                var doneMet = done.Count(timer => !timer.IsBreached);

                return (
                    Name: string.IsNullOrEmpty(group.Key) ? S["No account"].Value : accounts.GetValueOrDefault(group.Key) ?? group.Key,
                    Tickets: group.Select(timer => timer.Subject.Id).Distinct(StringComparer.Ordinal).Count(),
                    Done: done.Count,
                    Met: doneMet,
                    Late: group.Count(timer => timer.IsActive && timer.IsBreached));
            })
            .OrderByDescending(row => row.Done - row.Met + row.Late)
            .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(row => new ReportRow(
            [
                row.Name,
                ReportFormat.Number(row.Tickets),
                ReportFormat.Number(row.Done),
                ReportFormat.Number(row.Met),
                ReportFormat.Number(row.Done - row.Met),
                TicketReportData.Rate(row.Met, row.Done),
                ReportFormat.Number(row.Late),
            ]))
            .ToList();

        var section = ReportSection.ForTable(S["By account"].Value,
            [
                new ReportColumn(S["Account"].Value),
                new ReportColumn(S["Tickets"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Ended"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Met"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Breached"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Compliance"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Running, breached"].Value, ReportColumnAlign.End),
            ],
            rows);
        section.Description = S["How each account's targets went, the accounts with the most missed targets first."].Value;
        section.EmptyMessage = S["No service-level targets ended or ran in these days."].Value;

        return section;
    }

    private Task<List<ServiceLevelTimer>> ReadAsync(TicketScopedQuery scope, Func<IQueryIndex<ServiceLevelTimerIndex>, IQueryIndex<ServiceLevelTimerIndex>> filter, CancellationToken cancellationToken)
        => _data.ReadTimersAsync(scope, filter, cancellationToken);

    private async Task<Dictionary<string, string>> GetMetricNamesAsync(CancellationToken cancellationToken)
    {
        var provider = _scopes.FirstOrDefault(scope => scope.Name == TicketsConstants.ServiceLevelScope);

        return provider is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await provider.GetMetricsAsync(cancellationToken)).ToDictionary(option => option.Key, option => option.Name, StringComparer.Ordinal);
    }
}
