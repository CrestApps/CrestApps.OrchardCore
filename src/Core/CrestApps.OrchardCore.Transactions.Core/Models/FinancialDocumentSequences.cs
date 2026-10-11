namespace CrestApps.OrchardCore.Transactions.Core.Models;

/// <summary>
/// The last number issued in each financial-document series of a site, kept as one document.
/// </summary>
public sealed class FinancialDocumentSequences
{
    /// <summary>
    /// Gets or sets the document identifier assigned by the store.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets the last number issued per series, keyed by document kind and series.
    /// </summary>
    public Dictionary<string, long> Values { get; init; } = new(StringComparer.Ordinal);
}
