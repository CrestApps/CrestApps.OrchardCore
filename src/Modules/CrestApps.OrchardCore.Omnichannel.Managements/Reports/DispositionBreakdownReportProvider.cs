using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// The CRM disposition breakdown report: how completed activities were dispositioned in the period.
/// </summary>
public sealed class DispositionBreakdownReportProvider : OmnichannelReportBase
{
    private readonly ISession _session;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispositionBreakdownReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="campaignManager">The campaign manager.</param>
    /// <param name="dispositionManager">The disposition manager used to resolve disposition names.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DispositionBreakdownReportProvider(
        ISession session,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        INamedCatalogManager<OmnichannelDisposition> dispositionManager,
        IStringLocalizer<DispositionBreakdownReportProvider> stringLocalizer)
        : base(stringLocalizer)
    {
        _session = session;
        _campaignManager = campaignManager;
        _dispositionManager = dispositionManager;
    }

    /// <inheritdoc/>
    public override string Name => "omnichannel-disposition-breakdown";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Disposition breakdown"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["How completed CRM activities were dispositioned in the reporting period."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.ComplianceAudit;

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var completed = await OmnichannelReportQuery.GetCompletedAsync(
            _session,
            context.FromUtc,
            context.ToUtc,
            await OmnichannelReportFilter.GetCriteriaAsync(context.Filter, _campaignManager, cancellationToken),
            cancellationToken);
        var counts = OmnichannelReportAggregator.CountByDisposition(completed);
        var dispositions = await _dispositionManager.GetAllAsync(cancellationToken);
        var names = CatalogReportDisplayNames.ForDispositions(dispositions);
        var noDisposition = S["(No disposition)"].Value;
        var unknownDisposition = S["(Unknown disposition)"].Value;
        var total = counts.Values.Sum();

        var columns = new[]
        {
            new ReportColumn(S["Disposition"].Value),
            new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Share"].Value, ReportColumnAlign.End),
        };

        var rows = counts
            .OrderByDescending(entry => entry.Value)
            .Select(entry => new ReportRow(
            [
                CatalogReportDisplayNames.Resolve(entry.Key, names, noDisposition, unknownDisposition),
                ReportFormat.Number(entry.Value),
                ReportFormat.Percent(total > 0 ? (double)entry.Value / total : 0),
            ]))
            .ToList();

        rows.Add(new ReportRow(
        [
            S["All dispositions"].Value,
            ReportFormat.Number(total),
            ReportFormat.Percent(total > 0 ? 1d : 0),
        ], emphasize: true));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Dispositions"].Value, columns, rows));
    }
}
