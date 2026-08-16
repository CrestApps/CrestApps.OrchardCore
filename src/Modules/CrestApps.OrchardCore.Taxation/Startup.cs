using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Handlers;
using CrestApps.OrchardCore.Taxation.Migrations;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Registers the taxation framework services, the <see cref="TaxationPart"/>, and its content
/// integration.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTaxationCore();

        // The Addresses module replaces this with a content-backed implementation when enabled.
        services.TryAddScoped<ICountryService, DefaultCountryService>();

        services.AddContentPart<TaxationPart>()
            .UseDisplayDriver<TaxationPartDisplayDriver>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, TaxationPartSettingsDisplayDriver>();

        services.AddDataMigration<TaxationPartMigrations>();

        services.AddTaxableItemProvider<ContentItemTaxableItemProvider>();

        // Admin management UI for tax catalog entities (categories, jurisdictions, rules).
        services
            .AddDisplayDriver<TaxCategory, TaxCategoryDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<TaxCategory>, TaxCategoryHandler>();

        services
            .AddDisplayDriver<TaxType, TaxTypeDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<TaxType>, TaxTypeHandler>();

        services.AddDataMigration<TaxTypeMigrations>();

        services
            .AddDisplayDriver<TaxJurisdiction, TaxJurisdictionDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<TaxJurisdiction>, TaxJurisdictionHandler>();

        services
            .AddDisplayDriver<TaxRule, TaxRuleDisplayDriver>()
            .AddDisplayDriver<TaxRule, TaxRuleMethodDisplayDriver>()
            .AddScoped<ICatalogEntryHandler<TaxRule>, TaxRuleHandler>();

        services.AddTransient<IConfigureOptions<TaxCalculationMethodOptions>, TaxCalculationMethodOptionsConfiguration>();

        services.AddNavigationProvider<TaxationAdminMenu>();
        services.AddPermissionProvider<TaxationPermissionsProvider>();
    }
}
