namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Computes the amounts of an invoicing document, the same way for invoices, credit notes, estimates and recurring plans:
/// each line's net rounded to the currency, the document discounts spread over the lines in proportion, tax per tax and
/// rate on the summed net and spread back over the lines, prices that include tax split into net and tax, no tax for a
/// party that is not taxable, and cash rounding as an explicit amount.
/// </summary>
public interface IDocumentCalculator
{
    /// <summary>
    /// Lets the tax providers set the taxes of the lines when the context asks for automatic tax, then computes the
    /// amounts.
    /// </summary>
    /// <param name="context">The lines, charges and rules.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The tax breakdown and the totals.</returns>
    Task<DocumentCalculationResult> CalculateAsync(DocumentCalculationContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the amounts with the taxes the lines already carry. It is pure: the same input gives the same result.
    /// </summary>
    /// <param name="context">The lines, charges and rules.</param>
    /// <returns>The tax breakdown and the totals.</returns>
    DocumentCalculationResult Calculate(DocumentCalculationContext context);
}
