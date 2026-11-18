using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Rounds the tax lines of a calculation at a <see cref="TaxRoundingLevel"/>. The checkout's tax service and a document's
/// tax breakdown both round through it, so a cart and an invoice with the same lines round the same way at every level.
/// </summary>
public static class TaxLineRounding
{
    /// <summary>
    /// Rounds the unrounded amounts of <paramref name="lines"/> in place and returns the tax they come to.
    /// </summary>
    /// <param name="lines">The tax lines, in the order they were worked out, with their unrounded amounts.</param>
    /// <param name="level">The level to round at.</param>
    /// <param name="currency">The ISO-4217 currency, for rounding.</param>
    /// <param name="round">Rounds an amount in a currency, as the site's tax rounding strategy does.</param>
    public static decimal Apply(IList<TaxLine> lines, TaxRoundingLevel level, string currency, Func<decimal, string, decimal> round)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(round);

        if (lines.Count == 0)
        {
            return 0m;
        }

        if (level == TaxRoundingLevel.Rate)
        {
            return RoundPerRate(lines, currency, round);
        }

        if (level == TaxRoundingLevel.Line)
        {
            decimal total = 0m;

            foreach (var line in lines)
            {
                line.TaxAmount = round(line.TaxAmount, currency);
                total += line.TaxAmount;
            }

            return total;
        }

        var buckets = level switch
        {
            TaxRoundingLevel.Tax => lines.GroupBy(line => line.TaxType ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            TaxRoundingLevel.Jurisdiction => lines.GroupBy(line => line.JurisdictionId ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            _ => lines.GroupBy(_ => string.Empty),
        };

        decimal grandTotal = 0m;

        foreach (var bucket in buckets)
        {
            var bucketLines = bucket.ToArray();
            var roundedBucketTotal = round(bucketLines.Sum(line => line.TaxAmount), currency);

            decimal roundedLineSum = 0m;

            foreach (var line in bucketLines)
            {
                line.TaxAmount = round(line.TaxAmount, currency);
                roundedLineSum += line.TaxAmount;
            }

            var residual = roundedBucketTotal - roundedLineSum;

            if (residual != 0m)
            {
                bucketLines[^1].TaxAmount += residual;
            }

            grandTotal += roundedBucketTotal;
        }

        return grandTotal;
    }

    // Each tax and rate is rounded once on what its lines add up to, and spread back over them largest remainder first.
    private static decimal RoundPerRate(IList<TaxLine> lines, string currency, Func<decimal, string, decimal> round)
    {
        decimal total = 0m;

        foreach (var group in lines.GroupBy(line => (line.TaxCode ?? string.Empty, line.TaxName ?? string.Empty, line.Rate, line.IsCompound)))
        {
            var members = group.ToArray();
            var rounded = round(members.Sum(line => line.TaxAmount), currency);
            var shares = MoneyAllocation.Allocate(rounded, [.. members.Select(line => line.TaxAmount)], currency);

            for (var i = 0; i < members.Length; i++)
            {
                members[i].TaxAmount = shares[i];
            }

            total += rounded;
        }

        return total;
    }
}
