using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Grants the Supervisor role of a tenant already running Omnichannel the Omnichannel reports. The Supervisor
/// stereotype gained them, and Orchard Core only applies a stereotype when its feature is first enabled or the role is
/// created.
/// </summary>
internal sealed class SupervisorReportPermissionMigrations : DataMigration
{
    /// <summary>
    /// Schedules the grant.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public int Create()
    {
        // The roles are documents; they are written once this step's own transaction has committed.
        ShellScope.AddDeferredTask(scope => RolePermissionGrants.GrantToRoleAsync(
            scope.ServiceProvider,
            OmnichannelConstants.SupervisorRole,
            [OmnichannelConstants.Permissions.ViewReports]));

        return 1;
    }
}
