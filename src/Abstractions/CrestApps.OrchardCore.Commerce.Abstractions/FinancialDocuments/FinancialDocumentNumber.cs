namespace CrestApps.OrchardCore.Commerce.FinancialDocuments;

/// <summary>
/// An issued financial-document number. It pairs a tenant-scoped monotonic <see cref="Sequence"/> (for
/// internal ordering and gap detection) with a non-sequential <see cref="PublicToken"/> that is safe to show
/// to a customer without leaking document volume.
/// </summary>
public sealed class FinancialDocumentNumber
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FinancialDocumentNumber"/> class.
    /// </summary>
    /// <param name="sequence">The tenant-scoped monotonic sequence value within the requested series.</param>
    /// <param name="publicToken">The non-sequential public-facing identifier shown to customers.</param>
    public FinancialDocumentNumber(
        long sequence,
        string publicToken)
    {
        Sequence = sequence;
        PublicToken = publicToken;
    }

    /// <summary>
    /// Gets the tenant-scoped monotonic sequence value within the requested series. It is strictly increasing
    /// so gaps are detectable, and is intended for internal ordering rather than public display.
    /// </summary>
    public long Sequence { get; }

    /// <summary>
    /// Gets the non-sequential public-facing identifier. It is safe to show to a customer because it does not
    /// reveal how many documents a tenant has issued.
    /// </summary>
    public string PublicToken { get; }
}
