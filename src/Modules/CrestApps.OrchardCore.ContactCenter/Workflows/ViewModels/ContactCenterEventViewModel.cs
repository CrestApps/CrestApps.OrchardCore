namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// Represents the edit view model for the Contact Center event workflow activity.
/// </summary>
public class ContactCenterEventViewModel
{
    /// <summary>
    /// Gets or sets the domain event type to react to. When empty, the activity reacts to every event.
    /// </summary>
    public string EventType { get; set; }
}
