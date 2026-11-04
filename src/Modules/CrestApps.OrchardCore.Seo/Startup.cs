using CrestApps.Core.AI;
using CrestApps.OrchardCore.Seo.AI;
using CrestApps.OrchardCore.Seo.Drivers;
using CrestApps.OrchardCore.Seo.Handlers;
using CrestApps.OrchardCore.Seo.Liquid;
using CrestApps.OrchardCore.Seo.Migrations;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.Navigation;
using CrestApps.OrchardCore.Seo.Providers;
using CrestApps.OrchardCore.Seo.SchemaOrg;
using CrestApps.OrchardCore.Seo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Registers the structured data engine, the page's JSON-LD script, the site settings and the Schema.org part.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IStructuredDataManager, StructuredDataManager>()
            .AddScoped<IPageStructuredData, PageStructuredData>()
            .AddScoped<IStructuredDataProvider, ContentItemStructuredDataProvider>()
            .AddScoped<IStructuredDataProvider, SchemaOrgPartStructuredDataProvider>()
            .AddScoped<IStructuredDataProvider, SiteStructuredDataProvider>();

        services.AddScoped<IContentDisplayDriver, StructuredDataContentDriver>();
        services.AddScoped<IShapeDisplayEvents, StructuredDataScriptShapeEvents>();
        services.AddLiquidFilter<StructuredDataFilter>("structured_data");
        services
            .AddScoped<ContentItemUrlResolver>()
            .AddScoped<SchemaOrgContentSources>()
            .AddScoped<SchemaOrgConfigurator>();

        services
            .AddContentPart<SchemaOrgPart>()
            .UseDisplayDriver<SchemaOrgPartDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, SchemaOrgPartSettingsDisplayDriver>();
        services.AddDataMigration<SchemaOrgPartMigrations>();

        services
            .AddPermissionProvider<AdvancedSeoPermissionsProvider>()
            .AddSiteDisplayDriver<StructuredDataSettingsDisplayDriver>()
            .AddNavigationProvider<AdvancedSeoAdminMenu>();
        services.AddSiteSettingsPermission(StructuredDataConstants.SettingsGroupId, AdvancedSeoPermissionsProvider.ManageAdvancedSeoSettings);

    }
}

/// <summary>
/// Uses the SEO meta data editors set with Orchard Core's SEO module, and merges the JSON-LD they type there into
/// the page's single JSON-LD script, when that module is enabled.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Seo)]
public sealed class SeoStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IStructuredDataProvider, SeoMetaStructuredDataProvider>()
            .AddScoped<IStructuredDataProvider, EditorJsonLdStructuredDataProvider>()
            .AddScoped<IContentHandler, SeoAspectJsonLdHandler>();
    }
}

/// <summary>
/// Tells search engines how to search the site when Orchard Core's Search feature is enabled.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Search)]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IStructuredDataProvider, SearchActionStructuredDataProvider>();
    }
}

/// <summary>
/// Offers AI tools that configure the site's structured data when AI is enabled, so an agent, such as the admin's AI
/// chat, can describe a content type with schema.org.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AIStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIStartup(IStringLocalizer<AIStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetSchemaOrgOptionsTool>(GetSchemaOrgOptionsTool.TheName)
            .WithTitle(S["Get Schema.org Options"])
            .WithDescription(S["Lists the schema.org types and, for a content type, its structured data settings, its values and the suggested mappings."])
            .WithCategory(S["Search Engine Optimization"])
            .Selectable();

        services.AddCoreAITool<ConfigureContentTypeSchemaOrgTool>(ConfigureContentTypeSchemaOrgTool.TheName)
            .WithTitle(S["Configure Content Type Schema.org"])
            .WithDescription(S["Sets the schema.org type of a content type and maps its properties to the content type's fields."])
            .WithCategory(S["Search Engine Optimization"])
            .Selectable();

        services.AddCoreAITool<ConfigureSiteStructuredDataTool>(ConfigureSiteStructuredDataTool.TheName)
            .WithTitle(S["Configure Site Structured Data"])
            .WithDescription(S["Reads or changes the site's publisher, logo and profiles, and whether pages describe the web site."])
            .WithCategory(S["Search Engine Optimization"])
            .Selectable();
    }
}
