namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the direction of an interaction relative to the contact center.
/// </summary>
public enum InteractionDirection
{
    /// <summary>
    /// The customer initiated the interaction (for example, an inbound call).
    /// </summary>
    Inbound,

    /// <summary>
    /// The contact center initiated the interaction (for example, an outbound dial).
    /// </summary>
    Outbound,
}
