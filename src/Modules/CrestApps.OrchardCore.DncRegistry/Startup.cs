using CrestApps.OrchardCore.DncRegistry.Drivers;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DncRegistry;

/// <summary>
/// Registers core DNC Registry services and settings infrastructure.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<DncRegistrySettingsDisplayDriver>();
        services.AddNavigationProvider<DncRegistryAdminMenu>();
        services.AddPermissionProvider<DncRegistryPermissionProvider>();
    }
}

/// <summary>
/// Registers the USA FTC Do Not Call Registry integration.
/// </summary>
[Feature(DncRegistryConstants.Features.UsaFtc)]
public sealed class UsaFtcStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(UsaFtcDncRegistry));
        services.AddScoped<INationalDoNotCallRegistry, UsaFtcDncRegistry>();
        services.AddSiteDisplayDriver<UsaFtcDncRegistrySettingsDisplayDriver>();
        services.AddNavigationProvider<UsaFtcDncRegistryAdminMenu>();
    }
}

/// <summary>
/// Registers the Canada LNNTE-DNCL Registry integration.
/// </summary>
[Feature(DncRegistryConstants.Features.CanadaDncl)]
public sealed class CanadaDnclStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(CanadaDnclRegistry));
        services.AddScoped<INationalDoNotCallRegistry, CanadaDnclRegistry>();
        services.AddSiteDisplayDriver<CanadaDnclRegistrySettingsDisplayDriver>();
        services.AddNavigationProvider<CanadaDnclRegistryAdminMenu>();
    }
}
