namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Where a caller is in the menu, carried on the interaction's technical metadata so it survives a restart and
/// so a redelivered gather event can be recognised as one the caller has already been advanced by.
/// </summary>
public sealed class IvrFlowState
{
    /// <summary>
    /// Gets or sets the menu the caller is listening to.
    /// </summary>
    public string CurrentNodeId { get; set; }

    /// <summary>
    /// Gets or sets how many times the caller has failed to choose on this menu. It resets on entering a menu,
    /// because attempts belong to the menu: carrying them across would punish somebody for one fumbled key press
    /// at the top for the rest of the call.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the gather deliveries already applied. Provider webhooks are at-least-once, and acting twice
    /// on one key press would take a caller two levels into a menu they navigated once.
    /// </summary>
    public IList<string> AppliedDeliveryIds { get; set; } = [];
}
