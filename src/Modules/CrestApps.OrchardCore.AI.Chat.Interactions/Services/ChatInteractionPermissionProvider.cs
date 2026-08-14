using CrestApps.OrchardCore.AI.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

internal sealed class ChatInteractionPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ListChatInteractions,
        AIPermissions.ManageChatInteractionSettings,
        AIPermissions.ListChatInteractionsForOthers,
        AIPermissions.DeleteChatInteraction,
        AIPermissions.DeleteOwnChatInteraction,
        AIPermissions.EditChatInteractions,
        AIPermissions.EditOwnChatInteractions,
    ];

    /// <summary>
    /// Retrieves the permissions async.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
