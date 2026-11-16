namespace CrestApps.OrchardCore.Banking.Disbursements.Models;

/// <summary>
/// Where a batch is.
/// </summary>
public enum DisbursementBatchStatus
{
    /// <summary>
    /// Made, not released: its disbursements can still be taken out.
    /// </summary>
    Open,

    /// <summary>
    /// Being sent: the provider was called and did not answer yet, or failed after it may have sent. It is never sent
    /// again on its own; someone checks with the provider and reopens it (<c>ReopenReleasingBatchAsync</c>).
    /// </summary>
    Releasing,

    /// <summary>
    /// Released to its provider: its file is written, or its checks issued.
    /// </summary>
    Released,

    /// <summary>
    /// Every disbursement of it is settled or closed.
    /// </summary>
    Settled,

    /// <summary>
    /// Cancelled before release; its disbursements went back to pending.
    /// </summary>
    Canceled,
}
