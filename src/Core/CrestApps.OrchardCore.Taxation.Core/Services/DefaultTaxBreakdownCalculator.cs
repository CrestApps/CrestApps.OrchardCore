using System.Globalization;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// The default <see cref="ITaxBreakdownCalculator"/>, with EN 16931 arithmetic.
/// </summary>
/// <remarks>
/// <para>
/// Each item has a tax treatment: its own, else the document's. The breakdown has one row per treatment, tax code, name,
/// rate and compound flag. An item that is zero rated, exempt, reverse charged or out of scope, or taxable with no tax on
/// it, adds its net to a row of that treatment with no tax and its reason, also on an otherwise taxable document, so every
/// item has a category as EN 16931 asks.
/// </para>
/// <para>
/// Net prices: tax per tax and rate on the summed net, spread back over the items largest remainder first, then the
/// compound taxes on the net plus the other taxes. Prices that include tax: the gross of each group of items that share
/// their taxes is split once into net and tax. When no tax applies to an item whose price includes tax, there is nothing
/// to take out of it, so the whole price is the net.
/// </para>
/// <para>
/// That is rounding per tax and rate (<see cref="TaxRoundingLevel.Rate"/>). At any other level of
/// <see cref="TaxBreakdownRequest.RoundingLevel"/>, each item's taxes are worked out and rounded as the checkout's tax
/// service does it (<see cref="TaxLineRounding"/>), so a cart and an invoice with the same lines come to the same tax.
/// </para>
/// </remarks>
public sealed class DefaultTaxBreakdownCalculator : ITaxBreakdownCalculator
{
    private readonly ITaxRoundingStrategy _roundingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaxBreakdownCalculator"/> class.
    /// </summary>
    /// <param name="roundingStrategy">The site's tax rounding strategy, the one the checkout's tax service rounds with;
    /// without one, amounts are rounded to their currency's precision.</param>
    public DefaultTaxBreakdownCalculator(ITaxRoundingStrategy roundingStrategy = null)
    {
        _roundingStrategy = roundingStrategy;
    }

    /// <inheritdoc/>
    public TaxBreakdownResult Calculate(TaxBreakdownRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currency = request.Currency;
        var items = request.Items
            .Select(item => new Entry(item, Treat(item, request)))
            .ToList();

        // Per tax and rate on the summed net (EN 16931); at any other level, each item's tax is worked out and rounded as
        // the checkout's tax service rounds it, so a cart and an invoice with the same lines come to the same tax.
        var rows = request.RoundingLevel != TaxRoundingLevel.Rate
            ? CalculateByLevel(items, request)
            : request.PricesIncludeTax
                ? CalculateInclusive(items, currency)
                : CalculateExclusive(items, currency);

        // Every item charged no tax, by its treatment and reason: zero rated, exempt, reverse charged, out of scope, or
        // taxable with no tax on it. EN 16931 needs a breakdown row for each.
        foreach (var group in items
            .Where(entry => entry.Treatment != TaxTreatment.Taxable || entry.Taxes.Count == 0)
            .GroupBy(entry => (entry.Treatment, entry.Reason))
            .OrderBy(group => group.Key.Treatment))
        {
            rows.Add(new TaxBreakdownRow
            {
                Treatment = group.Key.Treatment,
                Rate = 0m,
                TaxableAmount = group.Sum(entry => entry.Net),
                TaxAmount = 0m,
                ExemptionReason = group.Key.Reason,
            });
        }

        var result = new TaxBreakdownResult();

        foreach (var row in rows)
        {
            result.Rows.Add(row);
        }

        foreach (var entry in items)
        {
            result.Items.Add(new TaxBreakdownItemResult
            {
                Id = entry.Item.Id,
                Net = entry.Net,
                BaseNet = entry.BaseNet,
                TaxAmount = entry.TaxAmount,
                Treatment = entry.Treatment,
                TreatmentReason = entry.Reason,
            });
        }

        result.TaxTotal = rows.Sum(row => row.TaxAmount);

        var total = items.Sum(entry => entry.Net) + result.TaxTotal;

        if (request.CashRoundingIncrement is > 0m and var increment)
        {
            var rounded = Math.Round(total / increment, MidpointRounding.AwayFromZero) * increment;

            result.RoundingAmount = rounded - total;
            total = rounded;
        }

        result.Total = total;

        return result;
    }

    // Each item's taxes worked out unrounded in the order the rules gave them (a compound tax on the net plus the taxes
    // before it, a price that includes tax divided by one plus its other rates), then rounded at the level through
    // TaxLineRounding, as the checkout's tax service does.
    private List<TaxBreakdownRow> CalculateByLevel(List<Entry> items, TaxBreakdownRequest request)
    {
        var currency = request.Currency;
        var lines = new List<TaxLine>();
        var owners = new List<(Entry Entry, TaxBreakdownRate Tax)>();

        for (var index = 0; index < items.Count; index++)
        {
            var entry = items[index];
            var gross = entry.Item.Amount;
            var net = request.PricesIncludeTax && entry.Taxes.Count > 0
                ? gross / (1m + (entry.Taxes.Where(tax => !tax.IsCompound).Sum(tax => tax.Rate) / 100m))
                : gross;
            var prior = 0m;

            entry.RawNet = net;

            foreach (var tax in entry.Taxes)
            {
                var basis = tax.IsCompound ? net + prior : net;
                var amount = basis * tax.Rate / 100m;

                lines.Add(new TaxLine
                {
                    ItemId = index.ToString(CultureInfo.InvariantCulture),
                    TaxCode = tax.Code,
                    TaxName = tax.Name,
                    TaxType = tax.TaxType,
                    JurisdictionId = tax.JurisdictionId,
                    Rate = tax.Rate,
                    IsCompound = tax.IsCompound,
                    TaxableAmount = basis,
                    TaxAmount = amount,
                });
                owners.Add((entry, tax));
                prior += amount;
            }
        }

        TaxLineRounding.Apply(lines, request.RoundingLevel, currency, Round);

        for (var i = 0; i < owners.Count; i++)
        {
            owners[i].Entry.TaxAmount += lines[i].TaxAmount;
        }

        foreach (var entry in items)
        {
            if (request.PricesIncludeTax)
            {
                entry.Net = entry.Item.Amount - entry.TaxAmount;

                // A line's discounts at the net, in the share its net is of its gross; a charge takes none.
                var ratio = entry.Item.Amount == 0m ? 1m : entry.RawNet / entry.Item.Amount;

                entry.BaseNet = entry.Item.IsCharge
                    ? entry.Net
                    : entry.Net + Round((entry.Item.BaseAmount - entry.Item.Amount) * ratio, currency);
            }
            else
            {
                entry.Net = entry.Item.Amount;
                entry.BaseNet = entry.Item.BaseAmount;
            }
        }

        // Each tax's taxable amount from the rounded figures: the item's net, plus its taxes before a compound one, so the
        // rows add up to the document's net and tax.
        var rows = new Dictionary<TaxKey, TaxBreakdownRow>();

        foreach (var entry in items)
        {
            var prior = 0m;

            for (var i = 0; i < owners.Count; i++)
            {
                if (owners[i].Entry != entry)
                {
                    continue;
                }

                var key = TaxKey.From(owners[i].Tax);

                if (!rows.TryGetValue(key, out var row))
                {
                    rows[key] = row = Row(key, 0m, 0m);
                }

                row.TaxableAmount += key.IsCompound ? entry.Net + prior : entry.Net;
                row.TaxAmount += lines[i].TaxAmount;
                prior += lines[i].TaxAmount;
            }
        }

        return [.. rows.Values.OrderBy(row => row.IsCompound).ThenBy(row => row.Code, StringComparer.Ordinal).ThenBy(row => row.Rate)];
    }

    private decimal Round(decimal amount, string currency)
        => _roundingStrategy?.Round(amount, currency) ?? CurrencyScale.Round(amount, currency);

    // Net prices: tax per tax and rate on the summed net, then the compound taxes on the net plus the other taxes.
    private List<TaxBreakdownRow> CalculateExclusive(List<Entry> items, string currency)
    {
        foreach (var entry in items)
        {
            entry.Net = entry.Item.Amount;
            entry.BaseNet = entry.Item.BaseAmount;
        }

        var rows = new List<TaxBreakdownRow>();

        foreach (var compound in new[] { false, true })
        {
            foreach (var group in items
                .SelectMany(entry => entry.Taxes.Where(tax => tax.IsCompound == compound).Select(tax => (Entry: entry, Tax: tax)))
                .GroupBy(pair => TaxKey.From(pair.Tax))
                .OrderBy(group => group.Key.Code, StringComparer.Ordinal)
                .ThenBy(group => group.Key.Rate))
            {
                var members = group.Select(pair => pair.Entry).ToList();
                var bases = members.Select(entry => compound ? entry.Net + entry.NonCompoundTax : entry.Net).ToArray();
                var taxableAmount = bases.Sum();
                var taxAmount = Round(taxableAmount * group.Key.Rate / 100m, currency);
                var shares = MoneyAllocation.Allocate(taxAmount, bases, currency);

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

    // Prices that include tax: the gross of each group of items that share their taxes is split once into net and tax.
    private List<TaxBreakdownRow> CalculateInclusive(List<Entry> items, string currency)
    {
        var rows = new Dictionary<TaxKey, TaxBreakdownRow>();

        foreach (var set in items.GroupBy(entry => TaxSet.From(entry.Taxes)))
        {
            var members = set.ToList();
            var taxes = set.Key.Taxes;
            var nonCompound = taxes.Where(tax => !tax.IsCompound).Sum(tax => tax.Rate) / 100m;
            var factor = (1m + nonCompound) * taxes.Where(tax => tax.IsCompound).Aggregate(1m, (product, tax) => product * (1m + (tax.Rate / 100m)));

            Split(members, factor, entry => entry.Item.Amount, currency, (entry, net) => entry.Net = net);

            // The net of each line before the document discounts, split the same way, so a line that took no discount
            // reports exactly the net it has after them. A charge takes no document discount: its net is its net.
            var lines = members.Where(entry => !entry.Item.IsCharge).ToList();

            Split(lines, factor, entry => entry.Item.BaseAmount, currency, (entry, net) => entry.BaseNet = net);

            foreach (var charge in members.Where(entry => entry.Item.IsCharge))
            {
                charge.BaseNet = charge.Net;
            }

            var gross = members.Sum(entry => entry.Item.Amount);
            var netSum = members.Sum(entry => entry.Net);
            var taxTotal = gross - netSum;

            foreach (var entry in members)
            {
                entry.TaxAmount = entry.Item.Amount - entry.Net;
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

    private void Split(List<Entry> members, decimal factor, Func<Entry, decimal> gross, string currency, Action<Entry, decimal> assign)
    {
        var grossValues = members.Select(gross).ToArray();
        var netSum = Round(grossValues.Sum() / factor, currency);
        var shares = MoneyAllocation.Allocate(netSum, grossValues, currency);

        for (var i = 0; i < members.Count; i++)
        {
            assign(members[i], shares[i]);
        }
    }

    private static TaxBreakdownRow Row(TaxKey key, decimal taxableAmount, decimal taxAmount)
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

    // How tax applies to an item: its own treatment, else the document's. A taxable one keeps its taxable taxes; when the
    // rules found only a zero rate or an exemption for it, it takes that treatment and reason instead.
    private static (TaxTreatment Treatment, string Reason, IReadOnlyList<TaxBreakdownRate> Taxes) Treat(TaxBreakdownItem item, TaxBreakdownRequest request)
    {
        var effective = item.Treatment ?? request.Treatment;
        var effectiveReason = string.IsNullOrWhiteSpace(item.TreatmentReason)
            ? (item.Treatment.HasValue ? null : request.ExemptionReason)
            : item.TreatmentReason;

        if (effective != TaxTreatment.Taxable)
        {
            return (effective, effectiveReason ?? request.ExemptionReason, []);
        }

        var all = item.Taxes ?? [];
        var taxable = all.Where(tax => tax is not null && tax.Treatment == TaxTreatment.Taxable).ToList();

        if (taxable.Count == 0 && all.FirstOrDefault(tax => tax is not null && tax.Treatment != TaxTreatment.Taxable) is { } untaxed)
        {
            return (untaxed.Treatment, untaxed.TreatmentReason, []);
        }

        return (TaxTreatment.Taxable, null, taxable);
    }

    private sealed class Entry
    {
        public Entry(TaxBreakdownItem item, (TaxTreatment Treatment, string Reason, IReadOnlyList<TaxBreakdownRate> Taxes) application)
        {
            Item = item;
            Treatment = application.Treatment;
            Reason = application.Reason;
            Taxes = application.Taxes;
        }

        public TaxBreakdownItem Item { get; }

        public TaxTreatment Treatment { get; }

        public string Reason { get; }

        public IReadOnlyList<TaxBreakdownRate> Taxes { get; }

        public decimal Net { get; set; }

        public decimal BaseNet { get; set; }

        // The net before rounding, for rounding at a level other than per rate.
        public decimal RawNet { get; set; }

        public decimal NonCompoundTax { get; set; }

        public decimal TaxAmount { get; set; }
    }

    private readonly record struct TaxKey(string Code, string Name, decimal Rate, bool IsCompound)
    {
        public static TaxKey From(TaxBreakdownRate tax)
            => new(tax.Code ?? string.Empty, tax.Name, tax.Rate, tax.IsCompound);
    }

    private sealed class TaxSet : IEquatable<TaxSet>
    {
        private readonly string _signature;

        private TaxSet(IReadOnlyList<TaxBreakdownRate> taxes, string signature)
        {
            Taxes = taxes;
            _signature = signature;
        }

        public IReadOnlyList<TaxBreakdownRate> Taxes { get; }

        public static TaxSet From(IReadOnlyList<TaxBreakdownRate> taxes)
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
