using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// The level the site rounds tax at, from its <see cref="TaxCalculationSettings"/> (Settings > Commerce > Tax
/// calculation): the one setting the checkout's tax service and invoices both read.
/// </summary>
public sealed class SiteTaxRoundingLevelProvider : ITaxRoundingLevelProvider
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteTaxRoundingLevelProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site settings.</param>
    public SiteTaxRoundingLevelProvider(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<TaxRoundingLevel> GetRoundingLevelAsync(CancellationToken cancellationToken = default)
        => (await _siteService.GetSettingsAsync<TaxCalculationSettings>()).RoundingLevel;
}
