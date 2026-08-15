using System;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Calculates tax as a single fixed amount per taxable line, independent of quantity.
/// </summary>
public sealed class FixedAmountTaxCalculationMethod : ITaxCalculationMethod
{
    /// <inheritdoc />
    public string Name => TaxCalculationMethodNames.FixedAmount;

    /// <inheritdoc />
    public TaxComputationResult Compute(TaxComputationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var amount = request.FixedAmount ?? 0m;

        return new TaxComputationResult
        {
            TaxableAmount = request.TaxableBase,
            TaxAmount = amount,
            EffectiveRate = 0m,
        };
    }
}
