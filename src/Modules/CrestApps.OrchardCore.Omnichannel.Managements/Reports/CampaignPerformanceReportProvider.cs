using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// The CRM campaign performance report: per-campaign completed-versus-pending activity progress.
/// </summary>
public sealed class CampaignPerformanceReportProvider : OmnichannelReportBase
{
    private readonly ISession _session;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ICatalogManager<OmnichannelCampaignGroup> _campaignGroupManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CampaignPerformanceReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="campaignManager">The campaign manager used to resolve campaign names.</param>
    /// <param name="campaignGroupManager">The campaign group manager used to aggregate campaigns.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CampaignPerformanceReportProvider(
        ISession session,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        ICatalogManager<OmnichannelCampaignGroup> campaignGroupManager,
        IStringLocalizer<CampaignPerformanceReportProvider> stringLocalizer)
        : base(stringLocalizer)
    {
        _session = session;
        _campaignManager = campaignManager;
        _campaignGroupManager = campaignGroupManager;
    }

    /// <inheritdoc/>
    public override string Name => "omnichannel-campaign-performance";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Campaign performance"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Per-campaign completed-versus-pending progress across the CRM activity inventory."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var activities = await OmnichannelReportQuery.GetCreatedAsync(
            _session,
            context.FromUtc,
            context.ToUtc,
            await OmnichannelReportFilter.GetCriteriaAsync(context.Filter, _campaignManager, cancellationToken),
            cancellationToken);
        var data = OmnichannelReportAggregator.BuildCampaignPerformance(activities);
        var campaigns = await _campaignManager.GetAllAsync(cancellationToken);
        var campaignGroups = await _campaignGroupManager.GetAllAsync(cancellationToken);
        var names = CatalogReportDisplayNames.ForCampaigns(campaigns);
        var groupNames = campaignGroups.ToDictionary(group => group.ItemId, group => group.DisplayText, StringComparer.Ordinal);
        var campaignGroupIds = campaigns.ToDictionary(campaign => campaign.ItemId, campaign => campaign.CampaignGroupId, StringComparer.Ordinal);
        var groupData = OmnichannelReportAggregator.BuildCampaignGroupPerformance(activities, campaignGroupIds);
        var noCampaign = S["(No campaign)"].Value;
        var unknownCampaign = S["(Unknown campaign)"].Value;

        var columns = new[]
        {
            new ReportColumn(S["Campaign"].Value),
            new ReportColumn(S["Total"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Pending"].Value, ReportColumnAlign.End),
            new ReportColumn(S["In progress"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Failed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Cancelled"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completion"].Value, ReportColumnAlign.End),
        };

        var rows = new List<ReportRow>();

        foreach (var row in data.Rows)
        {
            var name = CatalogReportDisplayNames.Resolve(row.CampaignId, names, noCampaign, unknownCampaign);

            rows.Add(new ReportRow(BuildCells(name, row.Counts)));
        }

        rows.Add(new ReportRow(BuildCells(S["All campaigns"].Value, data.Totals), emphasize: true));

        var groupRows = new List<ReportRow>();

        foreach (var row in groupData.Rows)
        {
            var name = CatalogReportDisplayNames.Resolve(
                row.CampaignGroupId,
                groupNames,
                S["(No campaign group)"].Value,
                S["(Unknown campaign group)"].Value);

            groupRows.Add(new ReportRow(BuildCells(name, row.Counts)));
        }

        groupRows.Add(new ReportRow(BuildCells(S["All campaign groups"].Value, groupData.Totals), emphasize: true));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Campaigns"].Value, columns, rows))
            .Add(ReportSection.ForTable(S["Campaign groups"].Value, columns, groupRows));
    }

    private static string[] BuildCells(string label, Models.Reports.OmnichannelProgressCounts counts)
    {
        return
        [
            label,
            ReportFormat.Number(counts.Total),
            ReportFormat.Number(counts.Completed),
            ReportFormat.Number(counts.Pending),
            ReportFormat.Number(counts.InProgress),
            ReportFormat.Number(counts.Failed),
            ReportFormat.Number(counts.Cancelled),
            ReportFormat.Percent(counts.CompletionRate),
        ];
    }
}
