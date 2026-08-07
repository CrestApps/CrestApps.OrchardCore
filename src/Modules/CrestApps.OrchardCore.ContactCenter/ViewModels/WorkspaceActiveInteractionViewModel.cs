namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the interaction the agent is currently handling, rendered as the active-call panel on the
/// agent desktop with the customer context and call state.
/// </summary>
public sealed class WorkspaceActiveInteractionViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the interaction.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity the interaction is linked to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the direction of the interaction relative to the contact center.
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the communication-session status of the interaction.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the customer label shown on the active-call panel.
    /// </summary>
    public string CustomerLabel { get; set; }

    /// <summary>
    /// Gets or sets the customer address (for example the phone number) of the interaction.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets the display name of the queue that handled the interaction, when applicable.
    /// </summary>
    public string QueueName { get; set; }

    /// <summary>
    /// Gets or sets the relative admin URL used to open the linked CRM contact record, when available.
    /// </summary>
    public string ContactUrl { get; set; }

    /// <summary>
    /// Gets or sets the admin URL used to complete the linked CRM activity through the shared
    /// Omnichannel completion experience.
    /// </summary>
    public string CompleteUrl { get; set; }

    /// <summary>
    /// Gets or sets the UTC time work on the interaction started.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the interaction was answered or connected.
    /// </summary>
    public DateTime? AnsweredUtc { get; set; }
}
