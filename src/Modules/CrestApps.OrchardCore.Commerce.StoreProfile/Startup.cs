using CrestApps.OrchardCore.Commerce.Core;
using CrestApps.OrchardCore.Commerce.StoreProfile.Drivers;
using CrestApps.OrchardCore.Commerce.StoreProfile.Navigation;
using CrestApps.OrchardCore.Commerce.Stores;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.Commerce.StoreProfile;

/// <summary>
/// Settings › Commerce › Store: the store profile, and the store's places for the features that read them.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStoreLocations();
        services.AddPermissionProvider<StoreProfilePermissionProvider>();
        services.AddSiteDisplayDriver<StoreProfileSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(StoreProfileSettingsDisplayDriver.GroupId, StoreProfilePermissions.ManageStoreProfile);
        services.AddNavigationProvider<StoreProfileAdminMenu>();
    }
}

/// <summary>
/// Exports the store profile with a deployment; Orchard Core's Settings recipe step imports it.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteSettingsPropertyDeploymentStep<StoreProfileSettings, DeploymentStartup>(
            S => S["Store profile"],
            S => S["The store's name, logo, tax ID, contact details and address."]);
    }
}
