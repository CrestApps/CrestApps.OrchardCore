namespace CrestApps.OrchardCore.Receipts.Models;

/// <summary>
/// Describes the settled state of the payment a receipt was issued for. This is a presentation concept
/// owned by the Receipts module so that consumers stay decoupled from any specific payment provider's
/// internal status values.
/// </summary>
public enum ReceiptStatus
{
    /// <summary>
    /// The payment was captured successfully.
    /// </summary>
    Paid,

    /// <summary>
    /// The payment has been initiated but is not yet confirmed.
    /// </summary>
    Pending,

    /// <summary>
    /// The payment did not complete successfully.
    /// </summary>
    Failed,
}
