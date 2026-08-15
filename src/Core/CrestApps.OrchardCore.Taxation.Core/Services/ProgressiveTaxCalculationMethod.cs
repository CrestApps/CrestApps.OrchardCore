using System;
using System.Linq;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Calculates tax using a progressive, tiered tax table. Each bracket taxes only the portion of the
/// taxable base that falls within it.
/// </summary>
public sealed class ProgressiveTaxCalculationMethod : ITaxCalculationMethod
{
    /// <inheritdoc />
    public string Name => TaxCalculationMethodNames.Progressive;

    /// <inheritdoc />
    public TaxComputationResult Compute(TaxComputationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tax = 0m;
        var rows = request.Table?.Rows;

        if (rows is not null)
        {
            foreach (var row in rows.OrderBy(r => r.Minimum))
            {
                if (request.TaxableBase <= row.Minimum)
                {
                    continue;
                }

                var upper = row.Maximum ?? request.TaxableBase;
                var portion = Math.Min(request.TaxableBase, upper) - row.Minimum;

                if (portion > 0m)
                {
                    tax += (portion * row.Rate) + row.FixedAmount;
                }
            }
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
