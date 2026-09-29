using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// The lead funnel: the leads created in the period, by the status they are in now, and how many of them were
/// converted.
/// </summary>
public sealed class LeadFunnelReportProvider : OmnichannelReportBase, IReportFilterMetadata
{
    private readonly ISession _session;
    private readonly INamedCatalog<LeadStatus> _statuses;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadFunnelReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="statuses">The lead status catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadFunnelReportProvider(
        ISession session,
        INamedCatalog<LeadStatus> statuses,
        IStringLocalizer<LeadFunnelReportProvider> stringLocalizer)
        : base(stringLocalizer)
    {
        _session = session;
        _statuses = statuses;
    }

    /// <inheritdoc/>
    /// <remarks>The report reads leads and opportunities, not activities, so only the period applies.</remarks>
    public IReadOnlyCollection<string> FilterNames => [];

    /// <inheritdoc/>
    public override string Name => "omnichannel-lead-funnel";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Lead funnel"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["The leads created in the period, by the status they are in now, with the share that was converted."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var from = range.FromUtc.GetValueOrDefault();
        var to = range.ToUtc.GetValueOrDefault(DateTime.MaxValue);

        var leads = (await _session.QueryIndex<LeadIndex>(index =>
                index.Latest &&
                index.CreatedUtc >= from &&
                index.CreatedUtc <= to)
            .ListAsync(cancellationToken))
            .ToList();

        var statuses = (await _statuses.GetAllAsync(cancellationToken)).ToDictionary(status => status.ItemId, StringComparer.Ordinal);
        var total = leads.Count;
        var converted = leads.Count(lead => lead.IsConverted);

        var columns = new[]
        {
            new ReportColumn(S["Status"].Value),
            new ReportColumn(S["Leads"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Share"].Value, ReportColumnAlign.End),
        };

        var rows = leads
            .GroupBy(lead => lead.StatusId ?? string.Empty)
            .Select(group => (
                Name: statuses.TryGetValue(group.Key, out var status) ? status.Name : S["(No status)"].Value,
                Order: statuses.TryGetValue(group.Key, out var ordered) ? ordered.Order : int.MaxValue,
                Count: group.Count()))
            .OrderBy(entry => entry.Order)
            .Select(entry => new ReportRow(
            [
                entry.Name,
                ReportFormat.Number(entry.Count),
                ReportFormat.Percent(total > 0 ? (double)entry.Count / total : 0),
            ]))
            .ToList();

        rows.Add(new ReportRow(
        [
            S["All leads"].Value,
            ReportFormat.Number(total),
            ReportFormat.Percent(total > 0 ? 1d : 0),
        ], emphasize: true));

        var summary = new[]
        {
            new ReportColumn(S["Measure"].Value),
            new ReportColumn(S["Value"].Value, ReportColumnAlign.End),
        };

        var summaryRows = new List<ReportRow>
        {
            new([S["Leads created"].Value, ReportFormat.Number(total)]),
            new([S["Converted"].Value, ReportFormat.Number(converted)]),
            new([S["Conversion rate"].Value, ReportFormat.Percent(total > 0 ? (double)converted / total : 0)]),
        };

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Summary"].Value, summary, summaryRows))
            .Add(ReportSection.ForTable(S["Leads by status"].Value, columns, rows));
    }
}
