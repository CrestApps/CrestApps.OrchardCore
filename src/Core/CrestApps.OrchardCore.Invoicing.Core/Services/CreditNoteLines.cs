using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Builds the lines of a credit note: part of an invoice line, kept at that line's own taxes so the credit's tax is
/// split by rate as the line's was, or an amount at the tax rates a payment's tax snapshot recorded.
/// </summary>
public static class CreditNoteLines
{
    /// <summary>
    /// Returns a credit line for part of an invoice line: one unit of <paramref name="amount"/>, at the line's taxes and
    /// tax treatment, crediting that line.
    /// </summary>
    /// <param name="line">The invoice line credited.</param>
    /// <param name="amount">The amount credited, before tax (or with tax, when the document's prices include it).</param>
    /// <param name="currency">The currency, for rounding.</param>
    /// <param name="name">The line's name, or <see langword="null"/> for the invoice line's.</param>
    public static DocumentLine ForPartOf(DocumentLine line, decimal amount, string currency, string name = null)
    {
        ArgumentNullException.ThrowIfNull(line);

        return new DocumentLine
        {
            Id = IdGenerator.GenerateId(),
            Kind = line.Kind,
            ProductId = line.ProductId,
            Sku = line.Sku,
            Name = name ?? line.Name,
            Description = line.Description,
            Quantity = 1m,
            Unit = line.Unit,
            UnitPrice = CurrencyScale.Round(amount, currency),
            TaxCategoryCode = line.TaxCategoryCode,
            Taxes = [.. line.Taxes ?? []],
            TaxTreatment = line.TaxTreatment,
            TaxTreatmentReason = line.TaxTreatmentReason,
            ServiceStart = line.ServiceStart,
            ServiceEnd = line.ServiceEnd,
            CreditedLineId = line.Id,
        };
    }

    /// <summary>
    /// Returns the credit lines for an amount refunded at the rates a tax snapshot recorded (the tax lines a refund
    /// calculator works out from it): one line per set of rates, with what was refunded before tax at those rates.
    /// </summary>
    /// <param name="taxLines">The refunded tax lines, per item and jurisdiction, with the taxable amount and rate
    /// (a fraction, such as 0.2 for 20%).</param>
    /// <param name="name">The name of each line.</param>
    /// <param name="pricesIncludeTax">Whether the credit note's prices include tax, so each line carries its tax.</param>
    /// <param name="currency">The currency, for rounding.</param>
    public static IReadOnlyList<DocumentLine> ForRefundedTax(IEnumerable<TaxLine> taxLines, string name, bool pricesIncludeTax, string currency)
    {
        // One item can be taxed by several jurisdictions on the same taxable amount: an item counts once, at its rates.
        var items = (taxLines ?? [])
            .Where(line => line.TaxableAmount > 0m)
            .GroupBy(line => line.ItemId ?? string.Empty, StringComparer.Ordinal)
            .Select(item => new
            {
                Taxable = item.Max(line => line.TaxableAmount),
                Tax = item.Sum(line => line.TaxAmount),
                Taxes = item
                    .Where(line => line.Rate > 0m)
                    .Select(line => new DocumentLineTax
                    {
                        Treatment = TaxTreatment.Taxable,
                        Code = line.TaxCode,
                        Name = line.TaxName,
                        Rate = line.Rate * 100m,
                        IsCompound = line.IsCompound,
                    })
                    .OrderBy(tax => tax.Code, StringComparer.Ordinal)
                    .ThenBy(tax => tax.Rate)
                    .ToArray(),
            });

        var lines = new List<DocumentLine>();

        foreach (var group in items.GroupBy(item => string.Join('|', item.Taxes.Select(tax => $"{tax.Code}:{tax.Rate}:{tax.IsCompound}")), StringComparer.Ordinal))
        {
            var taxes = group.First().Taxes;
            var taxable = group.Sum(item => item.Taxable);
            var amount = pricesIncludeTax ? taxable + group.Sum(item => item.Tax) : taxable;

            lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = DocumentLineKind.Fee,
                Name = taxes.Length == 0 ? name : $"{name} ({string.Join(", ", taxes.Select(tax => $"{tax.Rate:0.##}%"))})",
                Quantity = 1m,
                UnitPrice = CurrencyScale.Round(amount, currency),
                Taxes = [.. taxes],
                TaxTreatment = taxes.Length == 0 ? TaxTreatment.OutOfScope : TaxTreatment.Taxable,
                TaxTreatmentReason = taxes.Length == 0 ? name : null,
            });
        }

        return lines;
    }
}
