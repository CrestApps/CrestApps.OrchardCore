using CrestApps.OrchardCore.PageSections;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Registers Workforce's scheduling card on staff members, the Workforce settings of teams, activity codes, the Workforce calendar type and the admin screens.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(WorkforceConstants.CollectionName));
        services.AddDataMigration<WorkforceMigrations>()
            .AddIndexProvider<ActivityCodeIndexProvider>();

        services.AddWorkforceCore();

        // Workers are staff members: Workforce managers manage the staff of their teams, and Workforce admins everyone.
        services.AddStaffScopePermissions(WorkforcePermissions.ManageAllWorkforce, WorkforcePermissions.ManageWorkers);
        services.AddDisplayDriver<Team, WorkforceTeamDisplayDriver>();
        services.AddDisplayDriver<Team, WorkforceTeamMembersDisplayDriver>();
        services.AddDisplayDriver<Team, WorkforceTeamRowDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, WorkforceTeamsAuthorizationHandler>());
        services.AddDisplayDriver<StaffMember, WorkforceStaffDisplayDriver>();
        services.AddPageSection(StaffProfileSections.PageName, StaffProfileSections.Work, LocalizationSource.Create<Startup>("Work"), "fa-solid fa-briefcase", 40);
        services.AddDisplayDriver<ActivityCode, ActivityCodeRowDisplayDriver>();
        services.AddPermissionProvider<WorkforcePermissionsProvider>();
        services.AddNavigationProvider<WorkforceAdminMenu>();
    }
}
