namespace CrestApps.OrchardCore.Omnichannel.Managements.Models.Reports;

/// <summary>
/// Represents the aggregated data behind the CRM campaign performance report.
/// </summary>
public sealed class OmnichannelCampaignPerformanceData
{
    /// <summary>
    /// Gets or sets the per-campaign rows, ordered by total activities.
    /// </summary>
    public IList<OmnichannelCampaignRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the combined progress counts across every campaign.
    /// </summary>
    public OmnichannelProgressCounts Totals { get; set; } = new();
}
