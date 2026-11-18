namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// How much of an issued invoice is paid, mirrored from its transaction.
/// </summary>
public enum InvoiceSettlement
{
    /// <summary>
    /// Nothing is paid.
    /// </summary>
    Unpaid = 0,

    /// <summary>
    /// Part is paid; the rest is still owed.
    /// </summary>
    PartiallyPaid = 1,

    /// <summary>
    /// Nothing more is owed.
    /// </summary>
    Paid = 2,
}
