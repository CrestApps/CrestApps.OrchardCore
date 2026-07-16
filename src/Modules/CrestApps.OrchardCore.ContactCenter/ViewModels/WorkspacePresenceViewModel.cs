namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the agent's current presence, rendered by the agent desktop presence control.
/// </summary>
public sealed class WorkspacePresenceViewModel
{
    /// <summary>
    /// Gets or sets the current presence status name.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the optional reason code associated with the current presence status.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the pending presence status the system grants once in-flight routing completes.
    /// </summary>
    public string RequestedStatus { get; set; }
}
