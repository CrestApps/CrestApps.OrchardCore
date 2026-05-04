using CrestApps.OrchardCore.AI.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Services;

internal sealed class ChatSessionPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.DeleteChatSession,
        AIPermissions.DeleteAllChatSessions,
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
