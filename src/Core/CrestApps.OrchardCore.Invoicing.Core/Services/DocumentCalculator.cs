using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="IDocumentCalculator"/>. It works out each line's net, rounded to the currency, and spreads the
/// document discounts over the lines with a positive amount in proportion; everything about tax is the Taxation module's:
/// the tax providers set the taxes of the lines from its rules, and its <see cref="ITaxBreakdownCalculator"/> works out
/// the breakdown per tax and rate, each line's share, prices that include tax and cash rounding.
/// </summary>
/// <remarks>
/// A line discount is kept between nothing and the line amount, a percent between 0 and 100, so a preview never shows a
/// line that grows or goes negative; validation refuses such a discount before a document is saved.
/// </remarks>
public sealed class DocumentCalculator : IDocumentCalculator
{
    private readonly IEnumerable<IDocumentTaxProvider> _taxProviders;
    private readonly ITaxBreakdownCalculator _breakdown;
    private readonly ITaxRoundingLevelProvider _roundingLevelProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentCalculator"/> class.
    /// </summary>
    /// <param name="taxProviders">The providers that set the taxes of the lines from the tax rules.</param>
    /// <param name="breakdown">The Taxation module's breakdown calculator.</param>
    /// <param name="roundingLevelProvider">The level the site rounds tax at, the Taxation setting the checkout reads too;
    /// without one, a document rounds per tax and rate.</param>
    public DocumentCalculator(
        IEnumerable<IDocumentTaxProvider> taxProviders,
        ITaxBreakdownCalculator breakdown,
        ITaxRoundingLevelProvider roundingLevelProvider = null)
    {
        ArgumentNullException.ThrowIfNull(breakdown);

        _taxProviders = taxProviders ?? [];
        _breakdown = breakdown;
        _roundingLevelProvider = roundingLevelProvider;
    }

    /// <inheritdoc/>
    public async Task<DocumentCalculationResult> CalculateAsync(DocumentCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.RoundingLevel is null && _roundingLevelProvider is not null)
        {
            context.RoundingLevel = await _roundingLevelProvider.GetRoundingLevelAsync(cancellationToken);
        }

        if (context.AutomaticTax)
        {
            foreach (var provider in _taxProviders.OrderBy(provider => provider.Order))
            {
                if (await provider.ApplyTaxesAsync(context, cancellationToken))
                {
                    break;
                }
            }
        }

        return Calculate(context);
    }

    /// <inheritdoc/>
    public DocumentCalculationResult Calculate(DocumentCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currency = context.Currency;
        var lines = context.Lines ?? [];
        var charges = context.Charges ?? [];

        foreach (var line in lines)
        {
            line.Net = 0m;
            line.DocumentDiscount = 0m;
            line.TaxAmount = 0m;
            line.Total = 0m;
        }

        foreach (var charge in charges)
        {
            charge.Net = 0m;
            charge.TaxAmount = 0m;
        }

        // 1. Each line's amount less its own discount, rounded to the currency: net, or gross when prices include tax.
        var lineItems = lines
            .Where(line => line.HasAmount)
            .Select(line => new LineEntry(line, LineBase(line, currency)))
            .ToList();

        // 2. The document discounts, spread over the lines with a positive amount in proportion to it.
        var discountable = lineItems.Where(entry => entry.Base > 0m).ToList();
        var discountCharges = charges.Where(charge => charge.Kind == DocumentChargeKind.Discount).ToList();

        foreach (var discount in discountCharges)
        {
            var left = discountable.Sum(entry => entry.Amount);
            var amount = discount.Amount.HasValue
                ? Round(Math.Abs(discount.Amount.Value), currency)
                : Round(discountable.Sum(entry => entry.Base) * (discount.Percent ?? 0m) / 100m, currency);

            amount = Math.Min(Math.Max(0m, amount), Math.Max(0m, left));

            var shares = MoneyAllocation.Allocate(amount, [.. discountable.Select(entry => entry.Amount)], currency);

            for (var i = 0; i < discountable.Count; i++)
            {
                discountable[i].Amount -= shares[i];
            }

            discount.Net = amount;
        }

        // 3. Shipping and surcharges, with their own taxes.
        var chargeItems = charges
            .Where(charge => charge.Kind != DocumentChargeKind.Discount)
            .Select(charge => (Charge: charge, Amount: Round(Math.Abs(charge.Amount ?? 0m), currency)))
            .ToList();

        // 4. The tax: the Taxation module's breakdown of the lines and charges, with the taxes the rules gave them.
        var request = new TaxBreakdownRequest
        {
            Currency = currency,
            PricesIncludeTax = context.PricesIncludeTax,
            Treatment = context.TaxTreatment,
            ExemptionReason = context.ExemptionReason,
            CashRoundingIncrement = context.CashRoundingIncrement,
            RoundingLevel = context.RoundingLevel ?? TaxRoundingLevel.Rate,
        };

        foreach (var entry in lineItems)
        {
            request.Items.Add(Item(entry.Amount, entry.Base, isCharge: false, entry.Line.TaxTreatment, entry.Line.TaxTreatmentReason, entry.Line.Taxes));
        }

        foreach (var (charge, amount) in chargeItems)
        {
            request.Items.Add(Item(amount, amount, isCharge: true, charge.TaxTreatment, charge.TaxTreatmentReason, charge.Taxes));
        }

        var breakdown = _breakdown.Calculate(request);

        // 5. Write the amounts back.
        for (var i = 0; i < lineItems.Count; i++)
        {
            var line = lineItems[i].Line;
            var result = breakdown.Items[i];

            line.Net = result.BaseNet;
            line.DocumentDiscount = result.BaseNet - result.Net;
            line.TaxAmount = result.TaxAmount;
            line.Total = line.Net - line.DocumentDiscount + line.TaxAmount;
        }

        for (var i = 0; i < chargeItems.Count; i++)
        {
            var result = breakdown.Items[lineItems.Count + i];

            chargeItems[i].Charge.Net = result.Net;
            chargeItems[i].Charge.TaxAmount = result.TaxAmount;
        }

        if (context.PricesIncludeTax && discountCharges.Count > 0)
        {
            // Each discount is reported net of tax, in proportion to the gross it took off.
            var spread = lineItems.Sum(entry => entry.Line.DocumentDiscount);
            var nets = MoneyAllocation.Allocate(spread, [.. discountCharges.Select(charge => charge.Net)], currency);

            for (var i = 0; i < discountCharges.Count; i++)
            {
                discountCharges[i].Net = nets[i];
            }
        }

        return new DocumentCalculationResult
        {
            Taxes = [.. breakdown.Rows.Select(row => new DocumentTax
            {
                Code = row.Code,
                Name = row.Name,
                Treatment = row.Treatment,
                Rate = row.Rate,
                IsCompound = row.IsCompound,
                TaxableAmount = row.TaxableAmount,
                TaxAmount = row.TaxAmount,
                ExemptionReason = row.ExemptionReason,
            })],
            Totals = new DocumentTotals
            {
                Subtotal = lineItems.Sum(entry => entry.Line.Net),
                DiscountTotal = lineItems.Sum(entry => entry.Line.DocumentDiscount),
                ChargeTotal = chargeItems.Sum(item => item.Charge.Net),
                TaxTotal = breakdown.TaxTotal,
                RoundingAmount = breakdown.RoundingAmount,
                Total = breakdown.Total,
            },
        };
    }

    private static TaxBreakdownItem Item(decimal amount, decimal baseAmount, bool isCharge, TaxTreatment? treatment, string reason, IList<DocumentLineTax> taxes)
    {
        var item = new TaxBreakdownItem
        {
            Amount = amount,
            BaseAmount = baseAmount,
            IsCharge = isCharge,
            Treatment = treatment,
            TreatmentReason = reason,
        };

        foreach (var tax in taxes ?? [])
        {
            if (tax is null)
            {
                continue;
            }

            item.Taxes.Add(new TaxBreakdownRate
            {
                Treatment = tax.Treatment,
                TreatmentReason = tax.TreatmentReason,
                Code = tax.Code,
                Name = tax.Name,
                Rate = tax.Rate,
                IsCompound = tax.IsCompound,
                TaxType = tax.TaxType,
                JurisdictionId = tax.JurisdictionId,
            });
        }

        return item;
    }

    // A line's amount less its own discount. The discount is kept between nothing and the line amount, so a preview never
    // shows a line that grows or goes negative from it; validation refuses such a discount before the document is saved.
    private static decimal LineBase(DocumentLine line, string currency)
    {
        var amount = Round(line.Quantity * line.UnitPrice, currency);

        if (amount <= 0m)
        {
            return amount;
        }

        var discount = line.DiscountAmount.HasValue
            ? Round(line.DiscountAmount.Value, currency)
            : Round(amount * Math.Clamp(line.DiscountPercent ?? 0m, 0m, 100m) / 100m, currency);

        return Round(amount - Math.Clamp(discount, 0m, amount), currency);
    }

    private static decimal Round(decimal amount, string currency)
        => CurrencyScale.Round(amount, currency);

    private sealed class LineEntry
    {
        public LineEntry(DocumentLine line, decimal amount)
        {
            Line = line;
            Base = amount;
            Amount = amount;
        }

        public DocumentLine Line { get; }

        // Before the document discounts.
        public decimal Base { get; }

        // After the document discounts: net, or gross when prices include tax.
        public decimal Amount { get; set; }
    }
}
