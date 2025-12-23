using CrestApps.OrchardCore.AI.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

internal sealed class ChatInteractionPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ListChatInteractions,
        AIPermissions.ListChatInteractionsForOthers,
        AIPermissions.DeleteChatInteraction,
        AIPermissions.DeleteOwnChatInteraction,
        AIPermissions.EditChatInteractions,
        AIPermissions.EditOwnChatInteractions,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
