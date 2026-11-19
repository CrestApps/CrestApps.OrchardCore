namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// Whether an invoice reached its customer. It only moves forward.
/// </summary>
public enum InvoiceDelivery
{
    /// <summary>
    /// Not sent yet.
    /// </summary>
    NotSent = 0,

    /// <summary>
    /// Sent at least once.
    /// </summary>
    Sent = 1,

    /// <summary>
    /// Opened by the customer.
    /// </summary>
    Viewed = 2,
}
