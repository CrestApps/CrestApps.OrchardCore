using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Navigation;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Registers workers, teams, activity codes, the Workforce calendar type and the admin screens.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(WorkforceConstants.CollectionName));
        services.AddDataMigration<WorkforceMigrations>()
            .AddIndexProvider<WorkerIndexProvider>()
            .AddIndexProvider<TeamIndexProvider>()
            .AddIndexProvider<ActivityCodeIndexProvider>();

        services.AddWorkforceCore();
        services.AddDisplayDriver<WorkforceTeam, TeamRowDisplayDriver>();
        services.AddDisplayDriver<WorkerProfile, WorkerRowDisplayDriver>();
        services.AddDisplayDriver<ActivityCode, ActivityCodeRowDisplayDriver>();
        services.AddPermissionProvider<WorkforcePermissionsProvider>();
        services.AddNavigationProvider<WorkforceAdminMenu>();
    }
}
