using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents a single participant in an interaction, such as the customer, an agent, or a supervisor.
/// </summary>
public sealed class InteractionParticipant
{
    /// <summary>
    /// Gets or sets the role the participant plays in the interaction.
    /// </summary>
    public InteractionParticipantRole Role { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the participant, such as a user identifier for an agent.
    /// </summary>
    public string Identifier { get; set; }

    /// <summary>
    /// Gets or sets the display name of the participant.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the address of the participant, such as a phone number or email.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the participant joined the interaction.
    /// </summary>
    public DateTime? JoinedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the participant left the interaction.
    /// </summary>
    public DateTime? LeftUtc { get; set; }
}
