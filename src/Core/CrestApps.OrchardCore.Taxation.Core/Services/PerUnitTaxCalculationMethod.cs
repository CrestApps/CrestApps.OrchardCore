using System;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Calculates tax as a fixed amount charged for every unit of quantity.
/// </summary>
public sealed class PerUnitTaxCalculationMethod : ITaxCalculationMethod
{
    /// <inheritdoc />
    public string Name => TaxCalculationMethodNames.PerUnit;

    /// <inheritdoc />
    public TaxComputationResult Compute(TaxComputationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var amountPerUnit = request.FixedAmount ?? 0m;

        return new TaxComputationResult
        {
            TaxableAmount = request.TaxableBase,
            TaxAmount = amountPerUnit * request.Quantity,
            EffectiveRate = 0m,
        };
    }
}
