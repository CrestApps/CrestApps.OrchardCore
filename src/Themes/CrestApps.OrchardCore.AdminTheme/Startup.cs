using CrestApps.OrchardCore.AdminTheme.Deployments;
using CrestApps.OrchardCore.AdminTheme.Drivers;
using CrestApps.OrchardCore.AdminTheme.Migrations;
using CrestApps.OrchardCore.AdminTheme.Models;
using CrestApps.OrchardCore.AdminTheme.Navigation;
using CrestApps.OrchardCore.AdminTheme.Recipes;
using CrestApps.OrchardCore.AdminTheme.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.AdminTheme;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
        services.AddScoped<AdminColorSchemeStore>();
        services.AddScoped<AdminColorSchemeService>();
        services.AddSiteDisplayDriver<AdminThemeSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(AdminThemeSettingsDisplayDriver.GroupId, AdminPermissions.ManageAdminSettings);
        services.AddDisplayDriver<AdminColorScheme, AdminColorSchemeDisplayDriver>();
        services.AddNavigationProvider<AdminThemeAdminMenu>();
        services.AddDataMigration<AdminColorSchemesMigrations>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapAdminColorSchemeStylesheet();
    }
}

/// <summary>
/// Exports the admin theme's settings and the color schemes admins added. Orchard Core's Settings recipe step imports
/// the settings, the 'AdminColorSchemes' recipe step the schemes.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<AdminThemeSettings, DeploymentStartup>(
            S => S["Admin theme settings"],
            S => S["The default color scheme of the CrestApps admin theme."]);

        services.AddDeployment<AdminColorSchemesDeploymentSource, AdminColorSchemesDeploymentStep, SummaryDeploymentStepDisplayDriver<AdminColorSchemesDeploymentStep>>();
    }
}

/// <summary>
/// Imports the color schemes admins added.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AdminColorSchemesRecipeStep>();
    }
}

/// <summary>
/// Describes the admin theme's settings and color schemes to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AdminThemeSettingsSchema>();
        services.AddScoped<IRecipeStep, AdminColorSchemesRecipeStepSchema>();
        services.AddDeploymentStepSchema<AdminColorSchemesDeploymentStepSchema>();
    }
}
