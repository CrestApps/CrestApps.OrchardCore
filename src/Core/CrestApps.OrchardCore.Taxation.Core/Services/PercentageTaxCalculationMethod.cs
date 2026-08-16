using System;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Calculates tax as a percentage of the taxable base. Supports both tax-exclusive and tax-inclusive pricing.
/// </summary>
public sealed class PercentageTaxCalculationMethod : ITaxCalculationMethod
{
    /// <inheritdoc />
    public string Name => TaxCalculationMethodNames.Percentage;

    /// <inheritdoc />
    public TaxCalculationMethodInputs Inputs => TaxCalculationMethodInputs.Rate;

    /// <inheritdoc />
    public TaxComputationResult Compute(TaxComputationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rate = request.Rate ?? 0m;

        if (request.PriceIncludesTax)
        {
            var net = rate == -1m ? request.TaxableBase : request.TaxableBase / (1 + rate);
            var includedTax = request.TaxableBase - net;

            return new TaxComputationResult
            {
                TaxableAmount = net,
                TaxAmount = includedTax,
                EffectiveRate = rate,
            };
        }

        return new TaxComputationResult
        {
            TaxableAmount = request.TaxableBase,
            TaxAmount = request.TaxableBase * rate,
            EffectiveRate = rate,
        };
    }
}
