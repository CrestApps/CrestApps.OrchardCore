namespace CrestApps.OrchardCore.Banking.Disbursements.Models;

/// <summary>
/// What happened to a disbursement batch.
/// </summary>
public enum DisbursementBatchChangeKind
{
    /// <summary>
    /// It was made from pending disbursements.
    /// </summary>
    Created,

    /// <summary>
    /// It was released to its provider: its file is written, or its checks issued.
    /// </summary>
    Released,

    /// <summary>
    /// A batch stuck in sending was reopened to be sent again.
    /// </summary>
    Reopened,

    /// <summary>
    /// It was cancelled before release; its disbursements went back to pending.
    /// </summary>
    Canceled,

    /// <summary>
    /// Its disbursements were recorded as paid.
    /// </summary>
    Settled,
}
