using System.Globalization;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Exposes the tax rules and tax tables to the generic record tools (<c>listRecords</c>, <c>getRecord</c>) as the
/// read-only <c>taxRule</c> and <c>taxTable</c> kinds when the AI feature is on, under <c>ManageTaxation</c> as the
/// Taxation admin pages. A rule is created for one calculation method (its source), which the record tools cannot pass,
/// and its editor is two drivers on a source catalog; a table's rows are a list of objects the editor replay cannot post
/// yet. Both are changed on Commerce › Taxation or with the TaxRule and TaxTable recipe steps.
/// </summary>
[Feature(TaxationConstants.Feature.Taxation)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TaxRulesAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxRulesAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxRulesAIToolsStartup(IStringLocalizer<TaxRulesAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<TaxRule>("taxRule", kind => kind
            .WithTitle(S["Tax rule"])
            .WithDescription(S["a tax rule: how a tax is worked out (its calculation method: percentage, fixed amount, per unit, weight or volume, progressive, threshold or tax table), for which tax type, jurisdiction, category code and customer type, with its rate, amount or table, priority, whether it is included in the price, compounds, reverse-charges or applies to shipping, its thresholds and effective dates. Read-only here; rules are changed on Commerce › Taxation › Rules"])
            .WithPermission(TaxationPermissions.ManageTaxation)
            .FindUsing((services, id, cancellationToken) => services.GetRequiredService<INamedSourceCatalogManager<TaxRule>>().FindByIdAsync(id, cancellationToken))
            .ListUsing((services, query, cancellationToken) => services.GetRequiredService<INamedSourceCatalogManager<TaxRule>>().PageAsync(query.Page, query.PageSize, new QueryContext
            {
                Name = query.Text,
                Sorted = true,
            }, cancellationToken))
            .WithProjection(rule => new
            {
                id = rule.ItemId,
                name = rule.Name,
                method = rule.Source,
                enabled = rule.Enabled,
                priority = rule.Priority,
                taxType = rule.TaxType,
                taxName = rule.TaxName,
                taxCode = rule.TaxCode,
                jurisdictionId = rule.JurisdictionId,
                categoryCode = rule.CategoryCode,
                customerType = rule.CustomerType?.ToString(),
                rate = rule.Rate,
                fixedAmount = rule.FixedAmount,
                taxTableId = rule.TaxTableId,
                includedInPrice = rule.IncludedInPrice,
                isCompound = rule.IsCompound,
                reverseCharge = rule.ReverseCharge,
                appliesToShipping = rule.AppliesToShipping,
                minimumAmount = rule.MinimumAmount,
                maximumAmount = rule.MaximumAmount,
                thresholdBasis = rule.ThresholdBasis.ToString(),

                // Effective dates are pure dates, shown as entered.
                effectiveFrom = rule.EffectiveFromUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                effectiveTo = rule.EffectiveToUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            })
            .ReadOnly());

        services.AddAIRecordKind<TaxTable>("taxTable", kind => kind
            .WithTitle(S["Tax table"])
            .WithDescription(S["a tax table that table-based tax rules look their rate up in: its effective dates and rows of minimum, maximum, rate, fixed amount and base amount. Read-only here; tables are changed on Commerce › Taxation › Tables"])
            .WithPermission(TaxationPermissions.ManageTaxation)
            .UseCatalogManager<INamedCatalogManager<TaxTable>>()
            .WithProjection(table => new
            {
                id = table.ItemId,
                name = table.Name,
                effectiveFrom = table.EffectiveFromUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                effectiveTo = table.EffectiveToUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                rows = table.Rows.Select(row => new
                {
                    minimum = row.Minimum,
                    maximum = row.Maximum,
                    rate = row.Rate,
                    fixedAmount = row.FixedAmount,
                    baseAmount = row.BaseAmount,
                }),
            })
            .ReadOnly());
    }
}
