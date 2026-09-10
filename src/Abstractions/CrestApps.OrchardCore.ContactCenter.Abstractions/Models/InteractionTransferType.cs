namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies how a live interaction is transferred to a new destination.
/// </summary>
public enum InteractionTransferType
{
    /// <summary>
    /// The interaction is handed off immediately without the initiating agent speaking to the destination.
    /// </summary>
    Blind,

    /// <summary>
    /// The initiating agent consults the destination before completing the handoff (warm transfer).
    /// </summary>
    Consultative,
}
