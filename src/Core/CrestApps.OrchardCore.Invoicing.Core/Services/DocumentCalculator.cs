using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="IDocumentCalculator"/>, with EN 16931 arithmetic: each line's net is rounded to the currency;
/// the document discounts are spread over the lines with a positive amount in proportion; tax is computed per tax and rate
/// on the summed net and spread back over the lines, largest remainder first; prices that include tax keep the gross of
/// each group of lines that share their taxes and split it once into net and tax; cash rounding is an explicit amount.
/// </summary>
/// <remarks>
/// <para>
/// Each line and charge has a tax treatment: its own, else the document's (the bill-to's). The breakdown has one row per
/// treatment, tax code, rate and compound flag. A line that is zero rated, exempt, reverse charged or out of scope, or
/// taxable with no tax on it, adds its net to a row of that treatment with no tax and its reason, also on an otherwise
/// taxable document, so every line has a category as EN 16931 asks.
/// </para>
/// <para>
/// A price that includes tax is what the customer pays. When no tax applies to the line (a party that is exempt or
/// reverse charged, an exempt line), there is nothing to take out of it, so the whole price is the net: the customer pays
/// the same amount and the store does not lower its price because the tax is not due.
/// </para>
/// <para>
/// A line discount is kept between nothing and the line amount, a percent between 0 and 100, so a preview never shows a
/// line that grows or goes negative; validation refuses such a discount before a document is saved.
/// </para>
/// </remarks>
public sealed class DocumentCalculator : IDocumentCalculator
{
    private readonly IEnumerable<IDocumentTaxProvider> _taxProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentCalculator"/> class.
    /// </summary>
    /// <param name="taxProviders">The providers that may set the taxes of the lines when a document asks for automatic tax.</param>
    public DocumentCalculator(IEnumerable<IDocumentTaxProvider> taxProviders)
    {
        _taxProviders = taxProviders;
    }

    /// <inheritdoc/>
    public async Task<DocumentCalculationResult> CalculateAsync(DocumentCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

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
            .Select(line => new Item(line, LineBase(line, currency), Treat(line.Taxes, line.TaxTreatment, line.TaxTreatmentReason, context)))
            .ToList();

        // 2. The document discounts, spread over the lines with a positive amount in proportion to it.
        var discountable = lineItems.Where(item => item.Base > 0m).ToList();
        var discountCharges = charges.Where(charge => charge.Kind == DocumentChargeKind.Discount).ToList();

        foreach (var discount in discountCharges)
        {
            var left = discountable.Sum(item => item.Amount);
            var amount = discount.Amount.HasValue
                ? Round(Math.Abs(discount.Amount.Value), currency)
                : Round(discountable.Sum(item => item.Base) * (discount.Percent ?? 0m) / 100m, currency);

            amount = Math.Min(Math.Max(0m, amount), Math.Max(0m, left));

            var shares = Allocate(amount, [.. discountable.Select(item => item.Amount)], currency);

            for (var i = 0; i < discountable.Count; i++)
            {
                discountable[i].Amount -= shares[i];
                discountable[i].Discount += shares[i];
            }

            discount.Net = amount;
        }

        // 3. Shipping and surcharges, taxed with their own taxes.
        var chargeItems = charges
            .Where(charge => charge.Kind != DocumentChargeKind.Discount)
            .Select(charge => new Item(charge, Round(Math.Abs(charge.Amount ?? 0m), currency), Treat(charge.Taxes, charge.TaxTreatment, charge.TaxTreatmentReason, context)))
            .ToList();

        var items = lineItems.Concat(chargeItems).ToList();
        var rows = context.PricesIncludeTax
            ? CalculateInclusive(items, currency)
            : CalculateExclusive(items, currency);

        // Every line and charge that is charged no tax, by its treatment and reason: zero-rated, exempt, reverse charged,
        // out of scope, or taxable with no tax on it. EN 16931 needs a breakdown row for each.
        foreach (var group in items
            .Where(item => item.Treatment != TaxTreatment.Taxable || item.Taxes.Count == 0)
            .GroupBy(item => (item.Treatment, item.Reason))
            .OrderBy(group => group.Key.Treatment))
        {
            rows.Add(new DocumentTax
            {
                Treatment = group.Key.Treatment,
                Rate = 0m,
                TaxableAmount = group.Sum(item => item.Net),
                TaxAmount = 0m,
                ExemptionReason = group.Key.Reason,
            });
        }

        // 4. Write the amounts back.
        foreach (var item in lineItems)
        {
            item.Line.TaxAmount = item.TaxAmount;
            item.Line.Total = item.Line.Net - item.Line.DocumentDiscount + item.TaxAmount;
        }

        foreach (var item in chargeItems)
        {
            item.Charge.Net = item.Net;
            item.Charge.TaxAmount = item.TaxAmount;
        }

        if (context.PricesIncludeTax && discountCharges.Count > 0)
        {
            // Each discount is reported net of tax, in proportion to the gross it took off.
            var spread = lineItems.Sum(item => item.Line.DocumentDiscount);
            var nets = Allocate(spread, [.. discountCharges.Select(charge => charge.Net)], currency);

            for (var i = 0; i < discountCharges.Count; i++)
            {
                discountCharges[i].Net = nets[i];
            }
        }

        var totals = new DocumentTotals
        {
            Subtotal = lineItems.Sum(item => item.Line.Net),
            DiscountTotal = lineItems.Sum(item => item.Line.DocumentDiscount),
            ChargeTotal = chargeItems.Sum(item => item.Net),
            TaxTotal = rows.Sum(row => row.TaxAmount),
        };

        var total = totals.Subtotal - totals.DiscountTotal + totals.ChargeTotal + totals.TaxTotal;

        if (context.CashRoundingIncrement is > 0m and var increment)
        {
            var rounded = Math.Round(total / increment, MidpointRounding.AwayFromZero) * increment;

            totals.RoundingAmount = rounded - total;
            total = rounded;
        }

        totals.Total = total;

        return new DocumentCalculationResult
        {
            Taxes = rows,
            Totals = totals,
        };
    }

    // Net prices: tax per tax and rate on the summed net, then the compound taxes on the net plus the other taxes.
    private static List<DocumentTax> CalculateExclusive(List<Item> items, string currency)
    {
        foreach (var item in items)
        {
            item.Net = item.Amount;

            if (item.Line is { } line)
            {
                line.Net = item.Base;
                line.DocumentDiscount = item.Discount;
            }
        }

        var rows = new List<DocumentTax>();

        foreach (var compound in new[] { false, true })
        {
            foreach (var group in items
                .SelectMany(item => item.Taxes.Where(tax => tax.IsCompound == compound).Select(tax => (Item: item, Tax: tax)))
                .GroupBy(entry => TaxKey.From(entry.Tax))
                .OrderBy(group => group.Key.Code, StringComparer.Ordinal)
                .ThenBy(group => group.Key.Rate))
            {
                var members = group.Select(entry => entry.Item).ToList();
                var bases = members.Select(item => compound ? item.Net + item.NonCompoundTax : item.Net).ToArray();
                var taxableAmount = bases.Sum();
                var taxAmount = Round(taxableAmount * group.Key.Rate / 100m, currency);
                var shares = Allocate(taxAmount, bases, currency);

                for (var i = 0; i < members.Count; i++)
                {
                    members[i].TaxAmount += shares[i];

                    if (!compound)
                    {
                        members[i].NonCompoundTax += shares[i];
                    }
                }

                rows.Add(Row(group.Key, taxableAmount, taxAmount));
            }
        }

        return rows;
    }

    // Prices that include tax: the gross of each group of lines that share their taxes is split once into net and tax.
    private static List<DocumentTax> CalculateInclusive(List<Item> items, string currency)
    {
        var rows = new Dictionary<TaxKey, DocumentTax>();

        foreach (var set in items.GroupBy(item => TaxSet.From(item.Taxes)))
        {
            var members = set.ToList();
            var taxes = set.Key.Taxes;
            var nonCompound = taxes.Where(tax => !tax.IsCompound).Sum(tax => tax.Rate) / 100m;
            var factor = (1m + nonCompound) * taxes.Where(tax => tax.IsCompound).Aggregate(1m, (product, tax) => product * (1m + (tax.Rate / 100m)));

            Split(members, factor, item => item.Amount, currency, (item, net) => item.Net = net);

            // The net of each line before the document discounts, split the same way, so a line that took no discount
            // reports exactly the net it has after them.
            var lineMembers = members.Where(item => item.Line is not null).ToList();

            Split(lineMembers, factor, item => item.Base, currency, (item, net) =>
            {
                item.Line.Net = net;
                item.Line.DocumentDiscount = net - item.Net;
            });

            var gross = members.Sum(item => item.Amount);
            var netSum = members.Sum(item => item.Net);
            var taxTotal = gross - netSum;

            foreach (var item in members)
            {
                item.TaxAmount = item.Amount - item.Net;
            }

            if (taxes.Count == 0)
            {
                continue;
            }

            // The tax of the group, per tax: each rate on the summed net (a compound tax on the net plus the other taxes),
            // with the cent left by rounding given to the largest so the taxes add up to what the gross holds.
            var amounts = new decimal[taxes.Count];
            var nonCompoundAmount = 0m;

            for (var i = 0; i < taxes.Count; i++)
            {
                if (!taxes[i].IsCompound)
                {
                    amounts[i] = Round(netSum * taxes[i].Rate / 100m, currency);
                    nonCompoundAmount += amounts[i];
                }
            }

            for (var i = 0; i < taxes.Count; i++)
            {
                if (taxes[i].IsCompound)
                {
                    amounts[i] = Round((netSum + nonCompoundAmount) * taxes[i].Rate / 100m, currency);
                }
            }

            var largest = Array.IndexOf(amounts, amounts.Max());
            amounts[largest] += taxTotal - amounts.Sum();

            for (var i = 0; i < taxes.Count; i++)
            {
                var key = TaxKey.From(taxes[i]);
                var taxableAmount = taxes[i].IsCompound ? netSum + nonCompoundAmount : netSum;

                if (!rows.TryGetValue(key, out var row))
                {
                    rows[key] = row = Row(key, 0m, 0m);
                }

                row.TaxableAmount += taxableAmount;
                row.TaxAmount += amounts[i];
            }
        }

        return [.. rows.Values.OrderBy(row => row.IsCompound).ThenBy(row => row.Code, StringComparer.Ordinal).ThenBy(row => row.Rate)];
    }

    private static void Split(List<Item> members, decimal factor, Func<Item, decimal> gross, string currency, Action<Item, decimal> assign)
    {
        var grossValues = members.Select(gross).ToArray();
        var netSum = Round(grossValues.Sum() / factor, currency);
        var shares = Allocate(netSum, grossValues, currency);

        for (var i = 0; i < members.Count; i++)
        {
            assign(members[i], shares[i]);
        }
    }

    private static DocumentTax Row(TaxKey key, decimal taxableAmount, decimal taxAmount)
        => new()
        {
            Code = key.Code,
            Name = key.Name,
            Treatment = TaxTreatment.Taxable,
            Rate = key.Rate,
            IsCompound = key.IsCompound,
            TaxableAmount = taxableAmount,
            TaxAmount = taxAmount,
        };

    // How tax applies to a line or charge: its own treatment, else the document's. A taxable one keeps its taxable taxes;
    // when a tax provider found only a zero rate or an exemption for it, it takes that treatment and reason instead.
    private static TaxApplication Treat(IList<DocumentLineTax> taxes, TaxTreatment? treatment, string reason, DocumentCalculationContext context)
    {
        var effective = treatment ?? context.TaxTreatment;
        var effectiveReason = string.IsNullOrWhiteSpace(reason)
            ? (treatment.HasValue ? null : context.ExemptionReason)
            : reason;

        if (effective != TaxTreatment.Taxable)
        {
            return new TaxApplication(effective, effectiveReason ?? context.ExemptionReason, []);
        }

        var all = taxes ?? [];
        var taxable = all.Where(tax => tax is not null && tax.Treatment == TaxTreatment.Taxable).ToList();

        if (taxable.Count == 0 && all.FirstOrDefault(tax => tax is not null && tax.Treatment != TaxTreatment.Taxable) is { } untaxed)
        {
            return new TaxApplication(untaxed.Treatment, untaxed.TreatmentReason, []);
        }

        return new TaxApplication(TaxTreatment.Taxable, null, taxable);
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

    // Spreads a total over weights in proportion, in whole units of the currency, giving the units left by rounding down
    // to the largest remainders first (the earlier one on a tie), so the shares always add up to the total.
    private static decimal[] Allocate(decimal total, decimal[] weights, string currency)
    {
        var shares = new decimal[weights.Length];
        var weightSum = weights.Sum();

        if (weights.Length == 0 || total == 0m)
        {
            return shares;
        }

        if (weightSum == 0m)
        {
            shares[0] = total;

            return shares;
        }

        // Each exact share is rounded down to a unit, which leaves a remainder in [0, unit) whatever the signs of the total
        // and the weights; the remainders add up to a whole number of units, given back one each, largest first. So a
        // negative line beside positive ones (a deposit deduction) gets a negative share and the shares still add up.
        var unit = 1m / (decimal)Math.Pow(10, CurrencyScale.GetDecimalPlaces(currency));
        var remainders = new (decimal Remainder, int Index)[weights.Length];
        var allocated = 0m;

        for (var i = 0; i < weights.Length; i++)
        {
            var exact = total * weights[i] / weightSum;
            var floored = Math.Floor(exact / unit) * unit;

            shares[i] = floored;
            allocated += floored;
            remainders[i] = (exact - floored, i);
        }

        var left = (int)Math.Round((total - allocated) / unit);

        foreach (var (_, index) in remainders.OrderByDescending(entry => entry.Remainder).ThenBy(entry => entry.Index).Take(left))
        {
            shares[index] += unit;
        }

        return shares;
    }

    private readonly record struct TaxApplication(TaxTreatment Treatment, string Reason, IList<DocumentLineTax> Taxes);

    private sealed class Item
    {
        public Item(DocumentLine line, decimal amount, TaxApplication application)
        {
            Line = line;
            Base = amount;
            Amount = amount;
            Treatment = application.Treatment;
            Reason = application.Reason;
            Taxes = application.Taxes ?? [];
        }

        public Item(DocumentCharge charge, decimal amount, TaxApplication application)
        {
            Charge = charge;
            Base = amount;
            Amount = amount;
            Treatment = application.Treatment;
            Reason = application.Reason;
            Taxes = application.Taxes ?? [];
        }

        public TaxTreatment Treatment { get; }

        public string Reason { get; }

        public DocumentLine Line { get; }

        public DocumentCharge Charge { get; }

        // Before the document discounts.
        public decimal Base { get; }

        // After the document discounts: net, or gross when prices include tax.
        public decimal Amount { get; set; }

        public decimal Discount { get; set; }

        public IList<DocumentLineTax> Taxes { get; }

        public decimal Net { get; set; }

        public decimal NonCompoundTax { get; set; }

        public decimal TaxAmount { get; set; }
    }

    private readonly record struct TaxKey(string Code, string Name, decimal Rate, bool IsCompound)
    {
        public static TaxKey From(DocumentLineTax tax)
            => new(tax.Code ?? string.Empty, tax.Name, tax.Rate, tax.IsCompound);
    }

    private sealed class TaxSet : IEquatable<TaxSet>
    {
        private readonly string _signature;

        private TaxSet(IReadOnlyList<DocumentLineTax> taxes, string signature)
        {
            Taxes = taxes;
            _signature = signature;
        }

        public IReadOnlyList<DocumentLineTax> Taxes { get; }

        public static TaxSet From(IList<DocumentLineTax> taxes)
        {
            var ordered = (taxes ?? []).OrderBy(tax => tax.IsCompound).ThenBy(tax => tax.Code, StringComparer.Ordinal).ThenBy(tax => tax.Rate).ToArray();

            return new TaxSet(ordered, string.Join("|", ordered.Select(tax => $"{tax.Code}:{tax.Name}:{tax.Rate}:{tax.IsCompound}")));
        }

        public bool Equals(TaxSet other)
            => other is not null && string.Equals(_signature, other._signature, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => Equals(obj as TaxSet);

        public override int GetHashCode()
            => StringComparer.Ordinal.GetHashCode(_signature);
    }
}
