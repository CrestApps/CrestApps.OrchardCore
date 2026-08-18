namespace CrestApps.OrchardCore.Commerce.FinancialDocuments;

/// <summary>
/// The input to an <see cref="IFinancialDocumentPolicy"/>. It is a context object rather than bare
/// arguments so future ordering scenarios (a customer group, a tax context, or a document locale) can be
/// added without breaking the policy signature. It carries the canonical reference to the thing being
/// documented, the currency the money moved in, and the money event that triggered the evaluation.
/// </summary>
public sealed class FinancialDocumentContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FinancialDocumentContext"/> class.
    /// </summary>
    /// <param name="referenceType">The canonical reference type of the documented record (for example an order).</param>
    /// <param name="referenceId">The canonical reference identifier of the documented record.</param>
    /// <param name="currency">The ISO-4217 currency code the documented amount moved in.</param>
    /// <param name="reason">The money event that triggered the policy evaluation.</param>
    public FinancialDocumentContext(
        string referenceType,
        string referenceId,
        string currency,
        FinancialDocumentReason reason)
    {
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        Currency = currency;
        Reason = reason;
    }

    /// <summary>
    /// Gets the canonical reference type of the documented record (for example an order).
    /// </summary>
    public string ReferenceType { get; }

    /// <summary>
    /// Gets the canonical reference identifier of the documented record.
    /// </summary>
    public string ReferenceId { get; }

    /// <summary>
    /// Gets the ISO-4217 currency code the documented amount moved in.
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Gets the money event that triggered the policy evaluation.
    /// </summary>
    public FinancialDocumentReason Reason { get; }
}
