namespace CrestApps.OrchardCore.Omnichannel.Managements.Models.Reports;

/// <summary>
/// Represents the progress of a single campaign's activities in the campaign performance report.
/// </summary>
public sealed class OmnichannelCampaignRow
{
    /// <summary>
    /// Gets or sets the campaign identifier. An empty value represents activities with no campaign.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the progress counts for the campaign.
    /// </summary>
    public OmnichannelProgressCounts Counts { get; set; } = new();
}
