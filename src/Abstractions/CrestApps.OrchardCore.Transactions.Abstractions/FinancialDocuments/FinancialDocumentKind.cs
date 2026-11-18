namespace CrestApps.OrchardCore.Transactions.FinancialDocuments;

/// <summary>
/// The kinds of financial document that take their numbers from <see cref="IFinancialDocumentNumberGenerator"/>, each
/// from its own number series (<see cref="FinancialDocumentSeries"/>).
/// </summary>
public enum FinancialDocumentKind
{
    /// <summary>
    /// A receipt acknowledging a payment.
    /// </summary>
    Receipt,

    /// <summary>
    /// An invoice requesting payment: the payment request of a transaction, or an issued invoice document.
    /// </summary>
    Invoice,

    /// <summary>
    /// A credit note that reduces or cancels a previously issued invoice.
    /// </summary>
    CreditNote,

    /// <summary>
    /// A document that records a refund of a previously settled amount.
    /// </summary>
    RefundDocument,
}
