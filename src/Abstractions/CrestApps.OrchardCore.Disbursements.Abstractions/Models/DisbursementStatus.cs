namespace CrestApps.OrchardCore.Disbursements.Models;

/// <summary>
/// Where a disbursement is. <see cref="DisbursementStatuses.Transitions"/> says which moves are allowed.
/// </summary>
public enum DisbursementStatus
{
    /// <summary>
    /// Recorded, not in a batch yet.
    /// </summary>
    Pending,

    /// <summary>
    /// In a batch that is not released yet.
    /// </summary>
    Batched,

    /// <summary>
    /// Sent: in a released bank file, a printed check, or a provider payout.
    /// </summary>
    Submitted,

    /// <summary>
    /// Paid.
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
    /// Cancelled before it was sent.
    /// </summary>
    Canceled,

    /// <summary>
    /// Taken back after it settled.
    /// </summary>
    Reversed,
}
