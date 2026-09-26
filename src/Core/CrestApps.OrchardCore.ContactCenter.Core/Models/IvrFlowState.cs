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

    /// <summary>
    /// Gets or sets a value indicating whether the caller has left the menu. Anything the provider reports about a
    /// digit collection afterwards, such as a queue's callback offer, belongs to something else and must not move
    /// a caller who is already on their way.
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    /// Gets or sets the route the caller took through the menu, in order, so the call's history shows what they
    /// heard, what they pressed and where it sent them.
    /// </summary>
    public IList<IvrPathEntry> Path { get; set; } = [];
}

/// <summary>
/// One step of a caller's route through a phone menu.
/// </summary>
public sealed class IvrPathEntry
{
    /// <summary>
    /// Gets or sets the menu the step happened on.
    /// </summary>
    public string NodeId { get; set; }

    /// <summary>
    /// Gets or sets what the caller pressed, or <see langword="null"/> when the step is the menu being played.
    /// </summary>
    public string Digits { get; set; }

    /// <summary>
    /// Gets or sets what happened: the menu that was played, or the action that was taken.
    /// </summary>
    public string Result { get; set; }

    /// <summary>
    /// Gets or sets when it happened.
    /// </summary>
    public DateTime OccurredUtc { get; set; }
}
