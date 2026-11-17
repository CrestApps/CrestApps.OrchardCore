namespace CrestApps.OrchardCore.Banking.Disbursements.Models;

/// <summary>
/// A change to a disbursement batch, as the <see cref="Services.IDisbursementBatchObserver"/>s are told about it.
/// </summary>
public sealed class DisbursementBatchChange
{
    /// <summary>
    /// Gets or sets the batch as it is now.
    /// </summary>
    public DisbursementBatch Batch { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public DisbursementBatchChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the status before the change; <see langword="null"/> for a new batch.
    /// </summary>
    public DisbursementBatchStatus? PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets why, such as the reason a batch stuck in sending was reopened.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets how many of its disbursements the change settled.
    /// </summary>
    public int Settled { get; set; }

    /// <summary>
    /// Gets or sets the user name of who made the change, or <see langword="null"/> for the system.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the user id of who made the change, when a signed-in person made it.
    /// </summary>
    public string ActorId { get; set; }
}
