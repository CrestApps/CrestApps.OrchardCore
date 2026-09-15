namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents activity progress aggregated across a campaign group.
/// </summary>
public sealed class CampaignGroupSummaryRow
{
    /// <summary>
    /// Gets or sets the campaign group identifier.
    /// </summary>
    public string CampaignGroupId { get; set; }

    /// <summary>
    /// Gets or sets the resolved campaign group name.
    /// </summary>
    public string CampaignGroupName { get; set; }

    /// <summary>
    /// Gets or sets the activity progress counts.
    /// </summary>
    public ActivityProgressCounts Counts { get; set; } = new();
}
