namespace CrestApps.OrchardCore.Disbursements.Models;

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
