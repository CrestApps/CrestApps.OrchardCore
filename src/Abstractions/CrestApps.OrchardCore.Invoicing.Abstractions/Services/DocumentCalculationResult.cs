using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// What the document calculator computed: the tax breakdown and the totals. The line and charge amounts are written onto
/// the lines and charges of the context.
/// </summary>
public sealed class DocumentCalculationResult
{
    /// <summary>
    /// Gets the tax breakdown, one row per tax and rate.
    /// </summary>
    public IList<DocumentTax> Taxes { get; init; } = [];

    /// <summary>
    /// Gets the totals.
    /// </summary>
    public DocumentTotals Totals { get; init; } = new();
}
