namespace CrestApps.OrchardCore.Omnichannel.Managements.Models.Reports;

/// <summary>
/// Represents campaign performance aggregated by campaign group.
/// </summary>
public sealed class OmnichannelCampaignGroupPerformanceData
{
    /// <summary>
    /// Gets or sets the per-group rows.
    /// </summary>
    public IList<OmnichannelCampaignGroupRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the combined progress counts.
    /// </summary>
    public OmnichannelProgressCounts Totals { get; set; } = new();
}
