namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// A campaign an agent on the supervisor dashboard is signed in to, as the agent board's campaign filter offers it.
/// </summary>
public sealed class SupervisorCampaignViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the campaign.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the campaign.
    /// </summary>
    public string Name { get; set; }
}
