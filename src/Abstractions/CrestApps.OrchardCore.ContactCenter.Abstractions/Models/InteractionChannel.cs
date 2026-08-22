namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the channel an interaction is conducted on.
/// </summary>
public enum InteractionChannel
{
    /// <summary>
    /// A voice (telephone) interaction.
    /// </summary>
    Voice,

    /// <summary>
    /// An SMS/text interaction.
    /// </summary>
    Sms,

    /// <summary>
    /// An email interaction.
    /// </summary>
    Email,

    /// <summary>
    /// A web chat or messaging interaction.
    /// </summary>
    Chat,
}
