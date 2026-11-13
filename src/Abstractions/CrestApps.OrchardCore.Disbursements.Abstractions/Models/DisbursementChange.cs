namespace CrestApps.OrchardCore.Disbursements.Models;

/// <summary>
/// A change to a disbursement, as the <see cref="Services.IDisbursementObserver"/>s are told about it.
/// </summary>
public sealed class DisbursementChange
{
    /// <summary>
    /// Gets or sets the disbursement as it is now.
    /// </summary>
    public Disbursement Disbursement { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public DisbursementChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the status before the change; <see langword="null"/> for a new disbursement.
    /// </summary>
    public DisbursementStatus? PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets the user name of who made the change, or <see langword="null"/> for the system.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the user id of who made the change, when a signed-in person made it.
    /// </summary>
    public string ActorId { get; set; }
}
