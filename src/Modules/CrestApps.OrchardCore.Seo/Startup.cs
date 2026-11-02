using CrestApps.OrchardCore.Seo.Drivers;
using CrestApps.OrchardCore.Seo.Filters;
using CrestApps.OrchardCore.Seo.Handlers;
using CrestApps.OrchardCore.Seo.Navigation;
using CrestApps.OrchardCore.Seo.Providers;
using CrestApps.OrchardCore.Seo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Registers the structured data engine, the page's JSON-LD script, the site settings and the content type settings.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IStructuredDataManager, StructuredDataManager>()
            .AddScoped<IPageStructuredData, PageStructuredData>()
            .AddScoped<IStructuredDataProvider, ContentItemStructuredDataProvider>()
            .AddScoped<IStructuredDataProvider, ContentTypeMappingStructuredDataProvider>()
            .AddScoped<IStructuredDataProvider, SiteStructuredDataProvider>();

        services.AddScoped<IContentDisplayDriver, StructuredDataContentDriver>();
        services.AddScoped<IContentTypeDefinitionDisplayDriver, StructuredDataTypeSettingsDisplayDriver>();

        services
            .AddPermissionProvider<StructuredDataPermissionsProvider>()
            .AddSiteDisplayDriver<StructuredDataSettingsDisplayDriver>()
            .AddNavigationProvider<StructuredDataAdminMenu>();
        services.AddSiteSettingsPermission(StructuredDataConstants.SettingsGroupId, StructuredDataPermissionsProvider.ManageStructuredDataSettings);

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<StructuredDataScriptFilter>();
        });
    }
}

/// <summary>
/// Uses the SEO meta data editors set with Orchard Core's SEO module, and merges the JSON-LD they type there into
/// the page's single JSON-LD script, when that module is enabled.
/// </summary>
[RequireFeatures("OrchardCore.Seo")]
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
[RequireFeatures("OrchardCore.Search")]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IStructuredDataProvider, SearchActionStructuredDataProvider>();
    }
}
