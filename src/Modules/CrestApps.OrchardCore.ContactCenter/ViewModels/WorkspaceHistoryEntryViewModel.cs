namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents a recent interaction handled by the agent, rendered in the agent desktop history list.
/// </summary>
public sealed class WorkspaceHistoryEntryViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the interaction.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the direction of the interaction relative to the contact center.
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the final communication-session status of the interaction.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets when the call was answered, or <see langword="null"/> when it never was. With
    /// <see cref="EndedUtc"/> this is how long the agent was talking; a call that was never answered has no talk
    /// time to show.
    /// </summary>
    public DateTime? AnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets the customer label shown for the history entry.
    /// </summary>
    public string CustomerLabel { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction ended, when it has ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }
}
