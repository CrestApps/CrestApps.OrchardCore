using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// The primary entry point for calculating tax. Implementations orchestrate sourcing, jurisdiction
/// resolution, rule resolution, exemptions, nexus, and calculation to return a deterministic breakdown.
/// </summary>
public interface ITaxService
{
    /// <summary>
    /// Calculates the tax for the supplied context.
    /// </summary>
    /// <param name="context">The context that describes what, who, where, when, and how to tax.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A detailed <see cref="TaxCalculationResult"/> explaining the determination.</returns>
    Task<TaxCalculationResult> CalculateAsync(TaxCalculationContext context, CancellationToken cancellationToken = default);
}
