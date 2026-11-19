namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// The kind of an invoice.
/// </summary>
public enum InvoiceKind
{
    /// <summary>
    /// An invoice for goods or services.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// A charge related to an earlier invoice, such as a late fee.
    /// </summary>
    DebitNote = 1,

    /// <summary>
    /// A deposit invoiced before the work; the final invoice deducts it.
    /// </summary>
    Prepayment = 2,
}
