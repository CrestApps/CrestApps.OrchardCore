namespace CrestApps.OrchardCore.Commerce.FinancialDocuments;

/// <summary>
/// The input to an <see cref="IFinancialDocumentNumberGenerator"/>. It names the document kind being numbered
/// and an optional series so a tenant can keep independent sequences (for example a per-year or per-document
/// -kind series) without a signature change.
/// </summary>
public sealed class FinancialDocumentNumberRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FinancialDocumentNumberRequest"/> class.
    /// </summary>
    /// <param name="kind">The kind of document being numbered.</param>
    /// <param name="series">The optional series that scopes the sequence; <see langword="null"/> uses the default series.</param>
    public FinancialDocumentNumberRequest(
        FinancialDocumentKind kind,
        string series = null)
    {
        Kind = kind;
        Series = series;
    }

    /// <summary>
    /// Gets the kind of document being numbered.
    /// </summary>
    public FinancialDocumentKind Kind { get; }

    /// <summary>
    /// Gets the optional series that scopes the sequence. When <see langword="null"/>, the default series is
    /// used.
    /// </summary>
    public string Series { get; }
}
