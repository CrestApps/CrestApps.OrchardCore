using System;
using System.Linq;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Calculates tax by looking up the matching row of a tax table for the taxable base and applying its
/// rate and fixed amount.
/// </summary>
public sealed class TaxTableTaxCalculationMethod : ITaxCalculationMethod
{
    /// <inheritdoc />
    public string Name => TaxCalculationMethodNames.TaxTable;

    /// <inheritdoc />
    public TaxComputationResult Compute(TaxComputationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var row = request.Table?.Rows?.FirstOrDefault(r =>
            request.TaxableBase >= r.Minimum && (!r.Maximum.HasValue || request.TaxableBase < r.Maximum.Value));

        if (row is null)
        {
            return new TaxComputationResult
            {
                TaxableAmount = request.TaxableBase,
                TaxAmount = 0m,
                EffectiveRate = 0m,
            };
        }

        var tax = (request.TaxableBase * row.Rate) + row.FixedAmount;

        return new TaxComputationResult
        {
            TaxableAmount = request.TaxableBase,
            TaxAmount = tax,
            EffectiveRate = row.Rate,
        };
    }
}
