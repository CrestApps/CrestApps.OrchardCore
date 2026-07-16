namespace CrestApps.OrchardCore.Omnichannel.Managements.Models.Reports;

/// <summary>
/// Represents one campaign-group performance row.
/// </summary>
public sealed class OmnichannelCampaignGroupRow
{
    /// <summary>
    /// Gets or sets the campaign group identifier.
    /// </summary>
    public string CampaignGroupId { get; set; }

    /// <summary>
    /// Gets or sets the progress counts.
    /// </summary>
    public OmnichannelProgressCounts Counts { get; set; } = new();
}
