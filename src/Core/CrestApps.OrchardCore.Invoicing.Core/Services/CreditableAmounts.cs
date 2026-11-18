namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// What is still left to credit on an invoice: per line, by line id, and in total.
/// </summary>
public sealed class CreditableAmounts
{
    /// <summary>
    /// Gets what is left to credit on each line with an amount, by line id.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Lines { get; init; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>
    /// Gets what is left to credit on the whole invoice.
    /// </summary>
    public decimal Total { get; init; }
}
