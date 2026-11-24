using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Exposes the tax categories, tax types and tax jurisdictions to the generic record tools (<c>listRecords</c>,
/// <c>getRecord</c>, <c>saveRecord</c> and <c>deleteRecord</c>) when the AI feature is on. Writes replay the same
/// editors as the Taxation admin pages, with the same permission and the same named catalog managers.
/// </summary>
[Feature(TaxationConstants.Feature.Taxation)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<TaxCategory>("taxCategory", kind => kind
            .WithTitle(S["Tax category"])
            .WithDescription(S["a tax category (Electronics, Food...) that products and tax rules point to by its code; categories form a tree through their parent code"])
            .WithPermission(TaxationPermissions.ManageTaxation)
            .UseCatalogManager<INamedCatalogManager<TaxCategory>>()
            .WithProjection(category => new
            {
                id = category.ItemId,
                name = category.Name,
                code = category.Code,
                parentCode = category.ParentCode,
                description = category.Description,
                externalCodes = category.ExternalCodes is { Count: > 0 }
                    ? new Dictionary<string, string>(category.ExternalCodes, StringComparer.OrdinalIgnoreCase)
                    : null,
            })
            .WithInUseCheck(GetCategoryUseAsync)
            .UseEditorReplay());

        services.AddAIRecordKind<TaxType>("taxType", kind => kind
            .WithTitle(S["Tax type"])
            .WithDescription(S["a kind of tax (sales tax, VAT, excise...) that tax rules name"])
            .WithPermission(TaxationPermissions.ManageTaxation)
            .UseCatalogManager<INamedCatalogManager<TaxType>>()
            .WithProjection(type => new
            {
                id = type.ItemId,
                name = type.Name,
                description = type.Description,
            })
            .WithInUseCheck(GetTypeUseAsync)
            .UseEditorReplay());

        services.AddAIRecordKind<TaxJurisdiction>("taxJurisdiction", kind => kind
            .WithTitle(S["Tax jurisdiction"])
            .WithDescription(S["a place that levies tax (a country, state, county, city or special district), with its level, location and effective dates; tax rules apply in a jurisdiction"])
            .WithPermission(TaxationPermissions.ManageTaxation)
            .UseCatalogManager<INamedCatalogManager<TaxJurisdiction>>()
            .WithProjection(jurisdiction => new
            {
                id = jurisdiction.ItemId,
                name = jurisdiction.Name,
                code = jurisdiction.Code,
                level = jurisdiction.Level.ToString(),
                country = jurisdiction.Country,
                region = jurisdiction.Region,
                county = jurisdiction.County,
                city = jurisdiction.City,
                postalCode = jurisdiction.PostalCode,
                parentId = jurisdiction.ParentId,

                // Effective dates are pure dates, shown as entered.
                effectiveFrom = jurisdiction.EffectiveFromUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                effectiveTo = jurisdiction.EffectiveToUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            })
            .WithInUseCheck(GetJurisdictionUseAsync)
            .UseEditorReplay());
    }

    // As the tax tables page does before it deletes a table: a record the tax rules still point to is kept.
    private static async ValueTask<string> GetCategoryUseAsync(IServiceProvider services, TaxCategory category, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(category.Code))
        {
            return null;
        }

        var rules = (await services.GetRequiredService<INamedCatalog<TaxRule>>().GetAllAsync(cancellationToken))
            .Count(rule => string.Equals(rule.CategoryCode, category.Code, StringComparison.OrdinalIgnoreCase));

        var children = (await services.GetRequiredService<INamedCatalog<TaxCategory>>().GetAllAsync(cancellationToken))
            .Count(child => string.Equals(child.ParentCode, category.Code, StringComparison.OrdinalIgnoreCase));

        return Describe(rules, children, "categories");
    }

    private static async ValueTask<string> GetTypeUseAsync(IServiceProvider services, TaxType type, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(type.Name))
        {
            return null;
        }

        var rules = (await services.GetRequiredService<INamedCatalog<TaxRule>>().GetAllAsync(cancellationToken))
            .Count(rule => string.Equals(rule.TaxType, type.Name, StringComparison.OrdinalIgnoreCase));

        return Describe(rules, 0, null);
    }

    private static async ValueTask<string> GetJurisdictionUseAsync(IServiceProvider services, TaxJurisdiction jurisdiction, CancellationToken cancellationToken)
    {
        var rules = (await services.GetRequiredService<INamedCatalog<TaxRule>>().GetAllAsync(cancellationToken))
            .Count(rule => string.Equals(rule.JurisdictionId, jurisdiction.ItemId, StringComparison.OrdinalIgnoreCase));

        var children = (await services.GetRequiredService<INamedCatalog<TaxJurisdiction>>().GetAllAsync(cancellationToken))
            .Count(child => string.Equals(child.ParentId, jurisdiction.ItemId, StringComparison.OrdinalIgnoreCase));

        return Describe(rules, children, "jurisdictions");
    }

    private static string Describe(int rules, int children, string childName)
    {
        var uses = new List<string>();

        if (rules > 0)
        {
            uses.Add(string.Create(CultureInfo.InvariantCulture, $"{rules} tax rule(s)"));
        }

        if (children > 0)
        {
            uses.Add(string.Create(CultureInfo.InvariantCulture, $"{children} child {childName}"));
        }

        return uses.Count == 0
            ? null
            : $"It is still used by {string.Join(" and ", uses)}. Change or delete those first.";
    }
}
