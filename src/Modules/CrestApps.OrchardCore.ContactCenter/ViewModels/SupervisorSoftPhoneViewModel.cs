namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// What a supervisor's soft phone needs to show their own engagement: the hub it hears about it on.
/// </summary>
public sealed class SupervisorSoftPhoneViewModel
{
    /// <summary>
    /// Gets or sets the URL of the Contact Center real-time hub.
    /// </summary>
    public string HubUrl { get; set; }
}
