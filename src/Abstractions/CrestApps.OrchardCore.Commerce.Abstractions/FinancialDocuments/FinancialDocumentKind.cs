namespace CrestApps.OrchardCore.Commerce.FinancialDocuments;

/// <summary>
/// The kinds of financial document a money event can produce. The shipped default issues only a
/// <see cref="Receipt"/>; a future Orders domain can opt in to persisted <see cref="Invoice"/>,
/// <see cref="CreditNote"/>, and <see cref="RefundDocument"/> kinds through an
/// <see cref="IFinancialDocumentPolicy"/> without a breaking change.
/// </summary>
public enum FinancialDocumentKind
{
    /// <summary>
    /// A receipt acknowledging a payment. Produced on demand and never persisted as a numbered legal
    /// document. This is the only kind the shipped default policy issues.
    /// </summary>
    Receipt,

    /// <summary>
    /// A formal invoice requesting or recording payment for an order. Persisted and numbered when a policy
    /// opts in.
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
