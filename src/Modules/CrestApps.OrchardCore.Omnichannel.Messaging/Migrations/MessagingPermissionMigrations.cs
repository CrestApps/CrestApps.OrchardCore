using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;

/// <summary>
/// Brings the roles of a tenant already running the workspace in line with its permissions. Using the workspace used
/// to imply seeing every conversation; it now covers only the agent's own, and the unclaimed conversations of their
/// queues have a permission of their own. Every role that could use the workspace is granted that permission, so its
/// agents keep the shared inbox they had, while conversations claimed by their colleagues leave their view. The
/// Supervisor role is granted what its stereotype gained.
/// </summary>
internal sealed class MessagingPermissionMigrations : DataMigration
{
    /// <summary>
    /// Schedules the grants.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public int Create()
    {
        // The roles are documents; they are written once this step's own transaction has committed.
        ShellScope.AddDeferredTask(async scope =>
        {
            await RolePermissionGrants.GrantToRolesHoldingAsync(
                scope.ServiceProvider,
                MessagingPermissions.UseMessagingWorkspace,
                [MessagingPermissions.ViewQueueConversations]);

            await RolePermissionGrants.GrantToRoleAsync(
                scope.ServiceProvider,
                OmnichannelConstants.SupervisorRole,
                [MessagingPermissions.ViewQueueConversations, MessagingPermissions.SendDuringQuietHours]);
        });

        return 1;
    }
}
