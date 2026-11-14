namespace CrestApps.OrchardCore.Disbursements.Models;

/// <summary>
/// What happened to a disbursement.
/// </summary>
public enum DisbursementChangeKind
{
    /// <summary>
    /// It was recorded.
    /// </summary>
    Created,

    /// <summary>
    /// It was put in a batch, or taken out of one.
    /// </summary>
    Batched,

    /// <summary>
    /// Back to pending: its open batch was cancelled or reopened.
    /// </summary>
    Unbatched,

    /// <summary>
    /// It was sent.
    /// </summary>
    Submitted,

    /// <summary>
    /// It was paid.
    /// </summary>
    Settled,

    /// <summary>
    /// The bank sent it back.
    /// </summary>
    Returned,

    /// <summary>
    /// It could not be sent.
    /// </summary>
    Failed,

    /// <summary>
    /// It was cancelled.
    /// </summary>
    Canceled,

    /// <summary>
    /// It was taken back after it settled.
    /// </summary>
    Reversed,
}
