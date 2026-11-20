using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Gives the level the site rounds tax at (<see cref="TaxCalculationSettings"/>), which the checkout's tax service and
/// invoices both use.
/// </summary>
public interface ITaxRoundingLevelProvider
{
    /// <summary>
    /// Returns the level the site rounds tax at.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<TaxRoundingLevel> GetRoundingLevelAsync(CancellationToken cancellationToken = default);
}
