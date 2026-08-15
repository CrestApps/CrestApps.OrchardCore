using System;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Calculates tax as a fixed amount charged for every unit of weight.
/// </summary>
public sealed class PerWeightTaxCalculationMethod : ITaxCalculationMethod
{
    /// <inheritdoc />
    public string Name => TaxCalculationMethodNames.PerWeight;

    /// <inheritdoc />
    public TaxComputationResult Compute(TaxComputationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var amountPerWeight = request.FixedAmount ?? 0m;
        var weight = request.Weight ?? 0m;

        return new TaxComputationResult
        {
            TaxableAmount = request.TaxableBase,
            TaxAmount = amountPerWeight * weight,
            EffectiveRate = 0m,
        };
    }
}
