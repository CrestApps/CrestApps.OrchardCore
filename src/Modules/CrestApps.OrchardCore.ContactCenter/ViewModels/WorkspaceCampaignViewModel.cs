namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents a campaign the agent is signed in to, shown on the agent desktop.
/// </summary>
public sealed class WorkspaceCampaignViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the campaign.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the campaign, or its identifier when the campaign catalog does not have it.
    /// </summary>
    public string Name { get; set; }
}
