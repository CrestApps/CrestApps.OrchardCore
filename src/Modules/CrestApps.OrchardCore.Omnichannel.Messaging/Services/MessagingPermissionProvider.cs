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
        MessagingPermissions.ViewQueueConversations,
        MessagingPermissions.ViewAllConversations,
        MessagingPermissions.SendDuringQuietHours,
        MessagingPermissions.SendGroupMessages,
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
                // An agent works their own conversations and the unclaimed ones in the queues they serve. A conversation
                // a colleague has claimed, every conversation, and group messages are the supervisor's.
                Name = "Agent",
                Permissions =
                [
                    MessagingPermissions.UseMessagingWorkspace,
                    MessagingPermissions.ViewQueueConversations,
                ],
            },
            new PermissionStereotype
            {
                Name = "Supervisor",
                Permissions =
                [
                    MessagingPermissions.UseMessagingWorkspace,
                    MessagingPermissions.ViewQueueConversations,
                    MessagingPermissions.ViewAllConversations,
                    MessagingPermissions.SendGroupMessages,
                    MessagingPermissions.SendDuringQuietHours,
                    MessagingPermissions.ManageMessaging,
                ],
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
