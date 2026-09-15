namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the progress of a single campaign's activities: how many are completed versus how many
/// remain pending or in progress.
/// </summary>
public sealed class CampaignSummaryRow
{
    /// <summary>
    /// Gets or sets the campaign identifier. An empty value represents activities with no campaign.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the resolved campaign name.
    /// </summary>
    public string CampaignName { get; set; }

    /// <summary>
    /// Gets or sets the activity progress counts for the campaign.
    /// </summary>
    public ActivityProgressCounts Counts { get; set; } = new();
}
