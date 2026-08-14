using CrestApps.OrchardCore.DncRegistry.BackgroundTasks;
using CrestApps.OrchardCore.DncRegistry.Drivers;
using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Migrations;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using YesSql.Indexes;

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

/// <summary>
/// Registers the Local DNC Registry feature including storage, indexing, and admin UI.
/// </summary>
[Feature(DncRegistryConstants.Features.Local)]
public sealed class LocalDncRegistryStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(o => o.Collections.Add(DncRegistryConstants.CollectionName));

        services.AddSingleton<ILocalDncFileStore>(serviceProvider =>
        {
            var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>().Value;
            var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
            var logger = serviceProvider.GetRequiredService<ILogger<FileSystemStore>>();
            var path = Path.Combine(shellOptions.ShellsApplicationDataPath, shellOptions.ShellsContainerName, shellSettings.Name, "DncRegistry");
            var fileStore = new FileSystemStore(path, logger);

            return new LocalDncFileStore(fileStore);
        });

        services.Configure<StoreCollectionOptions>(options =>
        {
            options.Collections.Add(DncRegistryConstants.CollectionName);
        });

        services.AddScoped<INationalDoNotCallRegistry, LocalDncRegistry>();
        services.AddScoped<ILocalDncListManager, DefaultLocalDncListManager>();
        services.AddSingleton<IBackgroundTask, LocalDncImportBackgroundTask>();
        services.AddNavigationProvider<LocalDncRegistryAdminMenu>();
        services.AddDataMigration<LocalDncRegistryMigrations>();
        services.AddSingleton<IIndexProvider, LocalDncListIndexProvider>();
        services.AddSingleton<IIndexProvider, LocalDncEntryIndexProvider>();
        services.AddScoped<IDisplayDriver<LocalDncList>, LocalDncListDisplayDriver>();
        services.AddScoped<IDisplayDriver<LocalDncListOptions>, LocalDncListOptionsDisplayDriver>();
        services.AddScoped<IDisplayDriver<ImportLocalDncList>, ImportLocalDncListDisplayDriver>();
    }
}
