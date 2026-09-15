namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// View model for editing the Place Call or Send Message workflow activity.
/// </summary>
public sealed class StartOmnichannelActivityTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression resolving the CRM activity identifier to start.
    /// </summary>
    public string ActivityItemId { get; set; }
}
