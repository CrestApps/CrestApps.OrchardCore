using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Taxation.Migrations;

/// <summary>
/// Keeps rounding each line for a site that already charges tax. A new site rounds each tax and rate once on the summed
/// amount (the default of <see cref="TaxCalculationSettings"/>); a site that had tax rules before this setting existed
/// rounded each line, so it keeps doing that and no checkout total changes on upgrade. Staff can switch under
/// Settings > Commerce > Tax Calculation.
/// </summary>
internal sealed class TaxCalculationSettingsMigrations : DataMigration
{
    private readonly INamedCatalog<TaxRule> _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxCalculationSettingsMigrations"/> class.
    /// </summary>
    /// <param name="rules">The tax rules.</param>
    public TaxCalculationSettingsMigrations(INamedCatalog<TaxRule> rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// Stores line rounding for a site that has tax rules. The rules are looked at now, before a setup recipe adds any to
    /// a new site; the setting is saved once the tenant is ready.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        if ((await _rules.GetAllAsync()).Count > 0)
        {
            ShellScope.AddDeferredTask(scope => KeepLineRoundingAsync(scope.ServiceProvider));
        }

        return 1;
    }

    internal static async Task KeepLineRoundingAsync(IServiceProvider services)
    {
        var siteService = services.GetRequiredService<ISiteService>();
        var site = await siteService.LoadSiteSettingsAsync();

        // A level someone already chose is kept.
        if (site.Properties.ContainsKey(nameof(TaxCalculationSettings)))
        {
            return;
        }

        // The site rounded at the TaxationOptions level before this setting existed (Line unless the host configured
        // another), so that level is the one it keeps.
        var level = services.GetService<IOptions<TaxationOptions>>()?.Value?.RoundingLevel ?? TaxRoundingLevel.Line;

        site.Put(new TaxCalculationSettings
        {
            RoundingLevel = level,
        });

        await siteService.UpdateSiteSettingsAsync(site);
    }
}
