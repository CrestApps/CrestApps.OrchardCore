using System;
using System.Linq;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Calculates tax that only applies to the amount exceeding a configured threshold. The threshold and
/// rate are taken from the matching tax table row.
/// </summary>
public sealed class ThresholdTaxCalculationMethod : ITaxCalculationMethod
{
    /// <inheritdoc />
    public string Name => TaxCalculationMethodNames.Threshold;

    /// <inheritdoc />
    public TaxComputationResult Compute(TaxComputationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var row = request.Table?.Rows?
            .Where(r => request.TaxableBase >= r.Minimum && (!r.Maximum.HasValue || request.TaxableBase < r.Maximum.Value))
            .OrderByDescending(r => r.Minimum)
            .FirstOrDefault();

        if (row is null)
        {
            return new TaxComputationResult
            {
                TaxableAmount = request.TaxableBase,
                TaxAmount = 0m,
                EffectiveRate = 0m,
            };
        }

        var taxableExcess = request.TaxableBase - row.Minimum;
        var tax = (taxableExcess * row.Rate) + row.FixedAmount;

        if (tax < 0m)
        {
            tax = 0m;
        }

        var effectiveRate = request.TaxableBase > 0m ? tax / request.TaxableBase : 0m;

        return new TaxComputationResult
        {
            TaxableAmount = request.TaxableBase,
            TaxAmount = tax,
            EffectiveRate = effectiveRate,
        };
    }
}
