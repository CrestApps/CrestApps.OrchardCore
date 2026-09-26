using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Provides the permissions exposed by the Omnichannel Messaging workspace.
/// </summary>
internal sealed class MessagingPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        MessagingPermissions.ManageMessaging,
        MessagingPermissions.UseMessagingWorkspace,
        MessagingPermissions.SendDuringQuietHours,
        MessagingPermissions.SendGroupMessages,
        MessagingPermissions.ViewAllConversations,
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
                    MessagingPermissions.UseMessagingWorkspace,
                ],
            },
            new PermissionStereotype
            {
                Name = "Supervisor",
                Permissions =
                [
                    MessagingPermissions.UseMessagingWorkspace,
                    MessagingPermissions.SendGroupMessages,
                    MessagingPermissions.ViewAllConversations,
                    MessagingPermissions.ManageMessaging,
                ],
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
