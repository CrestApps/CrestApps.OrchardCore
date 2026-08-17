namespace CrestApps.OrchardCore.Transactions.Core;

/// <summary>
/// Holds the transaction sources registered by the enabled features. A module that creates transactions
/// registers its source through <c>AddTransactionSource</c> so the report can offer a source filter.
/// </summary>
public sealed class TransactionSourceOptions
{
    private readonly Dictionary<string, TransactionSource> _sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered transaction sources keyed by their technical source name.
    /// </summary>
    public IReadOnlyDictionary<string, TransactionSource> Sources
        => _sources;

    /// <summary>
    /// Adds or replaces a registered transaction source.
    /// </summary>
    /// <param name="source">The source to register.</param>
    public void AddSource(TransactionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _sources[source.Name] = source;
    }
}
