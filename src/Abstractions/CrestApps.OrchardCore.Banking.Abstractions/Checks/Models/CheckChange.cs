namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// A change to a check, as the <see cref="Services.ICheckObserver"/>s are told about it.
/// </summary>
public sealed class CheckChange
{
    /// <summary>
    /// Gets or sets the check as it is now.
    /// </summary>
    public CheckRecord Check { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public CheckChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the status before the change; <see langword="null"/> for a new check.
    /// </summary>
    public CheckStatus? PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets the check that replaced this one, for <see cref="CheckChangeKind.Reissued"/>.
    /// </summary>
    public CheckRecord ReplacedBy { get; set; }

    /// <summary>
    /// Gets or sets the user name of who made the change, or <see langword="null"/> for the system.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the user id of who made the change, when a signed-in person made it.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the date the check was voided, for <see cref="CheckChangeKind.Voided"/> and
    /// <see cref="CheckChangeKind.Spoiled"/>: the date a module that booked the payment reverses it on.
    /// </summary>
    public DateOnly? VoidedOn { get; set; }

    /// <summary>
    /// Gets or sets the date the bank paid the check, for <see cref="CheckChangeKind.Cleared"/>, or the date it was
    /// recorded as paid before, for <see cref="CheckChangeKind.Uncleared"/>.
    /// </summary>
    public DateOnly? ClearedOn { get; set; }

    /// <summary>
    /// Gets or sets what the check was before it was edited, for <see cref="CheckChangeKind.Updated"/>:
    /// <see cref="Check"/> holds what it is now.
    /// </summary>
    public CheckDetails PreviousDetails { get; set; }
}
