using System;
using System.Collections.Generic;
using System.Linq;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// The default <see cref="ITaxRefundCalculator"/>. It derives refund tax entirely from the original
/// immutable <see cref="TaxSnapshot"/> and never consults current tax rules, so historical transactions
/// are authoritative. Partial refunds are allocated proportionally across the original tax lines.
/// </summary>
public sealed class DefaultTaxRefundCalculator : ITaxRefundCalculator
{
    private readonly ITaxRoundingStrategy _roundingStrategy;

    public DefaultTaxRefundCalculator(ITaxRoundingStrategy roundingStrategy)
    {
        _roundingStrategy = roundingStrategy;
    }

    public TaxRefundResult CalculateFullRefund(TaxSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new TaxRefundResult
        {
            Currency = snapshot.Currency,
            RefundedTaxableAmount = snapshot.TaxableAmount,
            RefundedTaxAmount = snapshot.TaxAmount,
            RefundedTotalAmount = snapshot.TotalAmount,
            Lines = CloneLines(snapshot.Lines),
        };
    }

    public TaxRefundResult CalculateProportionalRefund(TaxSnapshot snapshot, decimal refundTotalAmount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // A non-positive refund returns nothing; a refund at or above the original total is a full refund
        // so it reproduces the snapshot exactly (no rounding drift from the proportional path).
        if (refundTotalAmount <= 0m || snapshot.TotalAmount <= 0m)
        {
            return new TaxRefundResult { Currency = snapshot.Currency };
        }

        if (refundTotalAmount >= snapshot.TotalAmount)
        {
            return CalculateFullRefund(snapshot);
        }

        var fraction = refundTotalAmount / snapshot.TotalAmount;
        var currency = snapshot.Currency;

        var lines = new List<TaxLine>();

        foreach (var line in snapshot.Lines ?? [])
        {
            var clone = CloneLine(line);
            clone.TaxableAmount = _roundingStrategy.Round(line.TaxableAmount * fraction, currency);
            clone.TaxAmount = _roundingStrategy.Round(line.TaxAmount * fraction, currency);
            lines.Add(clone);
        }

        return new TaxRefundResult
        {
            Currency = currency,
            RefundedTaxableAmount = _roundingStrategy.Round(snapshot.TaxableAmount * fraction, currency),
            RefundedTaxAmount = _roundingStrategy.Round(snapshot.TaxAmount * fraction, currency),
            RefundedTotalAmount = _roundingStrategy.Round(refundTotalAmount, currency),
            Lines = lines,
        };
    }

    private static List<TaxLine> CloneLines(IEnumerable<TaxLine> lines)
        => (lines ?? []).Select(CloneLine).ToList();

    private static TaxLine CloneLine(TaxLine line)
        => new()
        {
            ItemId = line.ItemId,
            TaxCode = line.TaxCode,
            TaxName = line.TaxName,
            TaxType = line.TaxType,
            JurisdictionId = line.JurisdictionId,
            JurisdictionName = line.JurisdictionName,
            Rate = line.Rate,
            TaxableAmount = line.TaxableAmount,
            TaxAmount = line.TaxAmount,
            CalculationMethod = line.CalculationMethod,
            IncludedInPrice = line.IncludedInPrice,
            IsCompound = line.IsCompound,
            RuleId = line.RuleId,
            RuleVersion = line.RuleVersion,
            TableId = line.TableId,
            TableVersion = line.TableVersion,
        };
}
