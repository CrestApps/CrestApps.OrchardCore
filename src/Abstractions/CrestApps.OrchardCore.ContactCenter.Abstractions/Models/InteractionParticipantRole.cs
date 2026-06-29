namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the role a participant plays in an interaction.
/// </summary>
public enum InteractionParticipantRole
{
    /// <summary>
    /// The external customer the interaction is with.
    /// </summary>
    Customer,

    /// <summary>
    /// An agent handling the interaction.
    /// </summary>
    Agent,

    /// <summary>
    /// A supervisor monitoring or assisting with the interaction.
    /// </summary>
    Supervisor,

    /// <summary>
    /// An automated system actor (for example, a dialer or virtual agent).
    /// </summary>
    System,

    /// <summary>
    /// An external party such as a third-party transfer target.
    /// </summary>
    External,
}
