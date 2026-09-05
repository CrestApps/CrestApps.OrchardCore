using CrestApps.OrchardCore.Sms.Workspace.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Sms.Workspace.Services;

/// <summary>
/// Provides the permissions exposed by the SMS Communication Portal.
/// </summary>
internal sealed class SmsWorkspacePermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        SmsWorkspacePermissions.ManageSmsNumberRoutes,
        SmsWorkspacePermissions.UseSmsPortal,
        SmsWorkspacePermissions.SendGroupSms,
        SmsWorkspacePermissions.ViewAllConversations,
    ];

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                Name = "Agent",
                Permissions =
                [
                    SmsWorkspacePermissions.UseSmsPortal,
                ],
            },
            new PermissionStereotype
            {
                Name = "Supervisor",
                Permissions =
                [
                    SmsWorkspacePermissions.UseSmsPortal,
                    SmsWorkspacePermissions.SendGroupSms,
                    SmsWorkspacePermissions.ViewAllConversations,
                    SmsWorkspacePermissions.ManageSmsNumberRoutes,
                ],
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
