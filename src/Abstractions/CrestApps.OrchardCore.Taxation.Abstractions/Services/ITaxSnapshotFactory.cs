using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Creates an immutable <see cref="TaxSnapshot"/> from a tax determination so that it can be stored
/// with a transaction and reproduced later.
/// </summary>
public interface ITaxSnapshotFactory
{
    /// <summary>
    /// Creates a snapshot from the supplied calculation result and context.
    /// </summary>
    /// <param name="context">The context the tax was determined from.</param>
    /// <param name="result">The calculation result to capture.</param>
    /// <returns>The created snapshot.</returns>
    TaxSnapshot Create(TaxCalculationContext context, TaxCalculationResult result);
}
