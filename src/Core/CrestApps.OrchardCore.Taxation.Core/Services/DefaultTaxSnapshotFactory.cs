using System;
using System.Linq;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxSnapshotFactory"/> that captures an immutable copy of a tax determination.
/// </summary>
public sealed class DefaultTaxSnapshotFactory : ITaxSnapshotFactory
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTaxSnapshotFactory"/> class.
    /// </summary>
    /// <param name="clock">The clock used to timestamp the snapshot.</param>
    public DefaultTaxSnapshotFactory(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public TaxSnapshot Create(TaxCalculationContext context, TaxCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        return new TaxSnapshot
        {
            CreatedUtc = _clock.UtcNow,
            TransactionDateUtc = context.TransactionDateUtc,
            Currency = result.Currency,
            TaxableAmount = result.TaxableAmount,
            TaxAmount = result.TaxAmount,
            TotalAmount = result.TotalAmount,
            Lines = result.Lines.Select(CloneLine).ToList(),
        };
    }

    private static TaxLine CloneLine(TaxLine line)
    {
        return new TaxLine
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
}
