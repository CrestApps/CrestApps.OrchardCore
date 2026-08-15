using System;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Calculates tax as a fixed amount charged for every unit of volume.
/// </summary>
public sealed class PerVolumeTaxCalculationMethod : ITaxCalculationMethod
{
    /// <inheritdoc />
    public string Name => TaxCalculationMethodNames.PerVolume;

    /// <inheritdoc />
    public TaxComputationResult Compute(TaxComputationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var amountPerVolume = request.FixedAmount ?? 0m;
        var volume = request.Volume ?? 0m;

        return new TaxComputationResult
        {
            TaxableAmount = request.TaxableBase,
            TaxAmount = amountPerVolume * volume,
            EffectiveRate = 0m,
        };
    }
}
