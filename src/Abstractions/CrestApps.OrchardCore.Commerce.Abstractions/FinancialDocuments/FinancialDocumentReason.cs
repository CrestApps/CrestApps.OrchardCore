namespace CrestApps.OrchardCore.Commerce.FinancialDocuments;

/// <summary>
/// The money event that asks an <see cref="IFinancialDocumentPolicy"/> which financial documents to issue.
/// The reasons mirror the settlement outcomes the Transactions domain already models, so a policy can decide
/// documents from a grounded event rather than a speculative one.
/// </summary>
public enum FinancialDocumentReason
{
    /// <summary>
    /// A payment settled the outstanding amount in full.
    /// </summary>
    PaymentSettled,

    /// <summary>
    /// A payment settled part of the outstanding amount and a balance remains.
    /// </summary>
    PartiallyPaid,

    /// <summary>
    /// A previously settled amount was refunded to the payer.
    /// </summary>
    Refunded,

    /// <summary>
    /// A previously settled amount was reversed by the payment network as a chargeback.
    /// </summary>
    ChargedBack,

    /// <summary>
    /// An outstanding balance was written off and will not be collected.
    /// </summary>
    WrittenOff,
}
