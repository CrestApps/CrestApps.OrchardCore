using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// The site's tax rounding level, set by a test. A new site's default: per tax and rate.
/// </summary>
public sealed class TestTaxRoundingLevelProvider : ITaxRoundingLevelProvider
{
    /// <summary>
    /// Gets or sets the level.
    /// </summary>
    public TaxRoundingLevel Level { get; set; } = new TaxCalculationSettings().RoundingLevel;

    /// <inheritdoc/>
    public Task<TaxRoundingLevel> GetRoundingLevelAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Level);
}
