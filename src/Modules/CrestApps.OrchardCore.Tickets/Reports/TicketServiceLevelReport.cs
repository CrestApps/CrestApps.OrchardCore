using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.Core.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Reports.DataSources;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// How the service-level targets of the reader's tickets went: for each metric, the targets that ended in the period,
/// how many were met and breached, and the ones running now that are already breached.
/// </summary>
internal sealed class TicketServiceLevelReport : IReport
{
    private readonly TicketReportData _data;
    private readonly TicketReportAccess _access;
    private readonly ISession _session;
    private readonly IEnumerable<IServiceLevelScopeProvider> _scopes;

    internal readonly IStringLocalizer S;

    public TicketServiceLevelReport(
        TicketReportData data,
        TicketReportAccess access,
        ISession session,
        IEnumerable<IServiceLevelScopeProvider> scopes,
        IStringLocalizer<TicketServiceLevelReport> stringLocalizer)
    {
        _data = data;
        _access = access;
        _session = session;
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

        return document.Add(table);
    }

    private async Task<List<ServiceLevelTimer>> ReadAsync(TicketScopedQuery scope, Func<IQueryIndex<ServiceLevelTimerIndex>, IQueryIndex<ServiceLevelTimerIndex>> filter, CancellationToken cancellationToken)
    {
        var timers = new List<ServiceLevelTimer>();
        var all = ReportIndexPages.ReadAsync<ServiceLevelTimer, ServiceLevelTimerIndex>(_session, filter, index => index.StartedUtc, ServiceLevelsConstants.CollectionName, cancellationToken);

        await foreach (var timer in _access.KeepVisibleAsync(scope, all, timer => timer.Subject.Id, cancellationToken))
        {
            timers.Add(timer);
        }

        return timers;
    }

    private async Task<Dictionary<string, string>> GetMetricNamesAsync(CancellationToken cancellationToken)
    {
        var provider = _scopes.FirstOrDefault(scope => scope.Name == TicketsConstants.ServiceLevelScope);

        return provider is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await provider.GetMetricsAsync(cancellationToken)).ToDictionary(option => option.Key, option => option.Name, StringComparer.Ordinal);
    }
}
