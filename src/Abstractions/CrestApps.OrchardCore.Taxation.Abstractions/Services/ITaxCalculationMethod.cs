using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Implements a single tax calculation strategy (for example percentage, fixed amount, or per unit).
/// Calculation methods are resolved by <see cref="Name"/> so third parties can register additional
/// strategies without modifying the framework.
/// </summary>
public interface ITaxCalculationMethod
{
    /// <summary>
    /// Gets the unique name of the calculation method.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Computes a tax amount for the supplied request.
    /// </summary>
    /// <param name="request">The computation request describing the taxable base and configuration.</param>
    /// <returns>The computed tax amount and effective rate.</returns>
    TaxComputationResult Compute(TaxComputationRequest request);
}
