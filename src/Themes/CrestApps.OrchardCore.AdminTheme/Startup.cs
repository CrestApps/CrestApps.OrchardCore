using CrestApps.OrchardCore.AdminTheme.Drivers;
using CrestApps.OrchardCore.AdminTheme.Models;
using CrestApps.OrchardCore.AdminTheme.Recipes;
using CrestApps.OrchardCore.AdminTheme.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.AdminTheme;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
        services.AddScoped<AdminColorSchemeService>();
        services.AddSiteDisplayDriver<AdminThemeSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(AdminThemeSettingsDisplayDriver.GroupId, AdminPermissions.ManageAdminSettings);
    }
}

/// <summary>
/// Exports the admin theme's settings with a deployment; Orchard Core's Settings recipe step imports them.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<AdminThemeSettings, DeploymentStartup>(
            S => S["Admin theme settings"],
            S => S["The default color scheme of the CrestApps admin theme."]);
    }
}

/// <summary>
/// Describes the admin theme's settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, AdminThemeSettingsSchema>();
    }
}
