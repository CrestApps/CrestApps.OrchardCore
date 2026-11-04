using CrestApps.OrchardCore.Teams.Core;
using CrestApps.OrchardCore.Teams.Core.Indexes;
using CrestApps.OrchardCore.Teams.Drivers;
using CrestApps.OrchardCore.Teams.Migrations;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Navigation;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// Registers the team hierarchy, its admin screens and its editor.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TeamsConstants.CollectionName));
        services.AddDataMigration<TeamsMigrations>()
            .AddIndexProvider<TeamIndexProvider>();

        services.AddTeamsCore();
        services.AddDisplayDriver<Team, TeamDisplayDriver>();
        services.AddDisplayDriver<Team, TeamRowDisplayDriver>();
        services.AddPermissionProvider<TeamsPermissionsProvider>();
        services.AddNavigationProvider<TeamsAdminMenu>();
    }
}
