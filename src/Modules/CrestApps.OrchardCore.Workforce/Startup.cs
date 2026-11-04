using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Navigation;
using CrestApps.OrchardCore.Workforce.PersonalData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Registers workers, the Workforce settings of teams, activity codes, the Workforce calendar type and the admin screens.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(WorkforceConstants.CollectionName));
        services.AddDataMigration<WorkforceMigrations>()
            .AddIndexProvider<WorkerIndexProvider>()
            .AddIndexProvider<ActivityCodeIndexProvider>();

        services.AddWorkforceCore();
        services.AddPersonalDataContributor<WorkerProfilePersonalDataContributor>();
        services.AddDisplayDriver<Team, WorkforceTeamDisplayDriver>();
        services.AddDisplayDriver<Team, WorkforceTeamRowDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, WorkforceTeamsAuthorizationHandler>());
        services.AddDisplayDriver<WorkerProfile, WorkerRowDisplayDriver>();
        services.AddDisplayDriver<User, UserWorkforceDisplayDriver>();
        services.AddScoped<IUserEventHandler, WorkerUserEventHandler>();
        services.AddDisplayDriver<ActivityCode, ActivityCodeRowDisplayDriver>();
        services.AddPermissionProvider<WorkforcePermissionsProvider>();
        services.AddNavigationProvider<WorkforceAdminMenu>();
    }
}
