using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Works out an invoice's tax breakdown and totals the one way issue does, so a draft saved in the editor shows the
/// amounts it will be issued with.
/// </summary>
public static class InvoiceCalculation
{
    /// <summary>
    /// Builds what the calculator reads from an invoice and the invoicing settings.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="settings">The invoicing settings, for cash rounding.</param>
    /// <param name="issueDate">The date the taxes are worked out for.</param>
    /// <returns>The calculation context.</returns>
    public static DocumentCalculationContext CreateContext(Invoice invoice, InvoicingSettings settings, DateOnly? issueDate)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return new DocumentCalculationContext
        {
            Currency = invoice.Currency?.Trim().ToUpperInvariant(),
            PricesIncludeTax = invoice.PricesIncludeTax,
            Lines = invoice.Lines,
            Charges = invoice.Charges,
            TaxTreatment = invoice.BillTo?.TaxTreatment ?? default,
            ExemptionReason = invoice.BillTo?.ExemptionReason,
            CashRoundingIncrement = settings?.CashRoundingIncrement,
            BillTo = invoice.BillTo,
            IssueDate = issueDate,
            AutomaticTax = true,
        };
    }

    /// <summary>
    /// Calculates the invoice and stores the tax breakdown and totals on it.
    /// </summary>
    /// <param name="calculator">The document calculator.</param>
    /// <param name="invoice">The invoice.</param>
    /// <param name="settings">The invoicing settings, for cash rounding.</param>
    /// <param name="issueDate">The date the taxes are worked out for.</param>
    /// <param name="cancellationToken">A token to cancel the calculation.</param>
    public static async Task ApplyAsync(IDocumentCalculator calculator, Invoice invoice, InvoicingSettings settings, DateOnly? issueDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calculator);

        var calculation = await calculator.CalculateAsync(CreateContext(invoice, settings, issueDate), cancellationToken);

        invoice.Taxes = calculation.Taxes;
        invoice.Totals = calculation.Totals;
    }
}
