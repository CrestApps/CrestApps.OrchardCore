namespace CrestApps.OrchardCore.ContactCenter.Workflows.ViewModels;

/// <summary>
/// Represents the edit view model for the recording workflow task activities, which act on a single interaction.
/// </summary>
public class RecordingInteractionTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression that resolves the interaction identifier to act on.
    /// </summary>
    public string InteractionId { get; set; }
}
