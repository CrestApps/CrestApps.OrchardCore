using CrestApps.OrchardCore.SiteResources.Services;
using CrestApps.OrchardCore.ThemeDesigner.Drivers;
using CrestApps.OrchardCore.ThemeDesigner.Endpoints;
using CrestApps.OrchardCore.ThemeDesigner.Filters;
using CrestApps.OrchardCore.ThemeDesigner.Navigation;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using CrestApps.OrchardCore.ThemeDesigner.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ThemeDesigner;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ThemeDesignDraftStore>();
        services.AddScoped<ThemeDesignPreview>();
        services.AddScoped<IThemeDesignDraftService, ThemeDesignDraftService>();
        services.AddScoped<IThemeShapeExplorer, ThemeShapeExplorer>();
        services.AddScoped<IThemeDraftPagePreviewer, ThemeDraftPagePreviewer>();

        // Previews request the site's own pages; a redirect (to the login page, say) is reported, not followed.
        services.AddHttpClient(ThemeDraftPagePreviewer.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddScoped<ISiteResourcesHandler, ThemeDesignSiteResourcesHandler>();

        // Shape binding resolvers and display events run in the order they are registered, and the first resolver
        // that returns a binding wins. The draft's resolver goes before Orchard Core's template resolvers, and the
        // draft's cache events before Dynamic Cache's.
        InsertFirst<IShapeBindingResolver, ThemeDesignPreviewShapeBindingResolver>(services);
        InsertFirst<IShapeDisplayEvents, ThemeDesignPreviewCacheEvents>(services);
        services.AddScoped<IShapeDisplayEvents, ThemeDesignShapeTraceEvents>();

        services.AddDisplayDriver<ThemeDesignChange, ThemeDesignChangeDisplayDriver>();
        services.Configure<MvcOptions>(options => options.Filters.Add<ThemeDesignPreviewFilter>());
        services.AddNavigationProvider<ThemeDesignerAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The draft's stylesheets and scripts, served to the designer who owns the draft.
        routes.MapGet("/" + ThemeDesignerConstants.DraftResourcesRoutePrefix + "/{fileName}", DraftResourceEndpoint.HandleAsync)
            .WithName("ThemeDesignerDraftResource");
    }

    private static void InsertFirst<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        var descriptor = ServiceDescriptor.Scoped<TService, TImplementation>();
        var index = 0;

        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ServiceType == typeof(TService))
            {
                index = i;

                break;
            }
        }

        services.Insert(index, descriptor);
    }
}
