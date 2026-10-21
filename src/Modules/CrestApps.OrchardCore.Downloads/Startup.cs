using CrestApps.OrchardCore.Downloads.Drivers;
using CrestApps.OrchardCore.Downloads.Handlers;
using CrestApps.OrchardCore.Downloads.Indexes;
using CrestApps.OrchardCore.Downloads.Migrations;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Navigation;
using CrestApps.OrchardCore.Downloads.Recipes;
using OrchardCore.Recipes;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using CrestApps.OrchardCore.Downloads.Reports;
using CrestApps.OrchardCore.Reports;

namespace CrestApps.OrchardCore.Downloads;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The entitlements live in their own collection, which has to be declared for its documents to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(DownloadsConstants.CollectionName));
        services.AddDataMigration<DownloadMigrations>()
            .AddIndexProvider<DownloadEntitlementIndexProvider>();

        // The files sit under the tenant's App_Data, which no URL serves. Each upload goes through the file creation
        // pipeline first, whose handlers can be scoped.
        services.TryAddTransient<FileCreationService>();
        services.AddScoped<IDigitalFileStore>(serviceProvider =>
        {
            var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>().Value;
            var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
            var path = Path.Combine(shellOptions.ShellsApplicationDataPath, shellOptions.ShellsContainerName, shellSettings.Name, DownloadsConstants.StoreFolder);

            return new DigitalFileStore(
                new FileSystemStore(path, serviceProvider.GetRequiredService<ILogger<FileSystemStore>>()),
                serviceProvider.GetRequiredService<FileCreationService>());
        });

        services.AddScoped<DownloadService>();
        services.AddScoped<IOrderEventHandler, DownloadOrderEventHandler>();
        services.AddScoped<IOrderEmailSectionProvider, DownloadEmailSectionProvider>();
        services.AddDisplayDriver<Order, OrderDownloadsDisplayDriver>();

        services.AddContentPart<DigitalFilesPart>()
            .UseDisplayDriver<DigitalFilesPartDisplayDriver>();

        services.AddSiteDisplayDriver<DownloadSettingsDisplayDriver>();
        services.AddNavigationProvider<DownloadSettingsAdminMenu>();
        services.AddNavigationProvider<DownloadsAccountMenu>();
        services.AddPermissionProvider<DownloadsPermissionsProvider>();
        services.AddSiteSettingsPermission(DownloadsConstants.SettingsGroupId, DownloadsPermissions.ManageDownloads);
    }
}

/// <summary>
/// Puts sample files on products from a recipe.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<DigitalFilesStep>();
}

/// <summary>
/// Adds the downloads report to Reports > Commerce.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddReport<DownloadsReport>();
}
