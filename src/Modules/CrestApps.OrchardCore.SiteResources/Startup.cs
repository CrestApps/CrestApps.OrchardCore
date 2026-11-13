using CrestApps.Core.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.SiteResources.Deployments;
using CrestApps.OrchardCore.SiteResources.Drivers;
using CrestApps.OrchardCore.SiteResources.Endpoints;
using CrestApps.OrchardCore.SiteResources.Filters;
using CrestApps.OrchardCore.SiteResources.Handlers;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Navigation;
using CrestApps.OrchardCore.SiteResources.Recipes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.SiteResources;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteResourcesCore();

        services
            .AddScoped<ICatalogEntryHandler<SiteResource>, SiteResourceHandler>()
            .AddDisplayDriver<SiteResource, SiteResourceDisplayDriver>();

        // Adds the global resources to every page and lets templates ask for the on-demand ones by name.
        services.Configure<MvcOptions>(options => options.Filters.Add<SiteResourcesFilter>());

        services.AddPermissionProvider<SiteResourcesPermissionsProvider>();
        services.AddNavigationProvider<SiteResourcesAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The served resources. A front-end resource is public; the address carries its version, so browsers keep it
        // until it changes.
        routes.MapGet("/" + SiteResourcesConstants.RoutePrefix + "/{fileName}", SiteResourceEndpoint.HandleAsync)
            .WithName("SiteResource");
    }
}

[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<SiteResourcesStep>();
    }
}

[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<SiteResourcesDeploymentSource, SiteResourcesDeploymentStep>();
    }
}

/// <summary>
/// Describes the SiteResources recipe step to CrestApps Recipes, so the recipe editor and the AI can validate it.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, SiteResourcesRecipeStepSchema>();
    }
}
