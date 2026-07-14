namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Describes an optional dialer profile contributed to Omnichannel activity management.
/// </summary>
public sealed class ActivityDialerProfileDescriptor
{
    /// <summary>
    /// Gets or sets the profile identifier.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the profile display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the activity source applied by the profile.
    /// </summary>
    public string ActivitySource { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier applied by the profile.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the implementation-defined routing target used when enqueueing activities.
    /// </summary>
    public string RoutingTargetId { get; set; }
}
