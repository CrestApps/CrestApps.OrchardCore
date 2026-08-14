using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Claude;

/// <summary>
/// Provides claude permission functionality.
/// </summary>
public sealed class ClaudePermissionProvider : IPermissionProvider
{
    public static readonly Permission ManageClaudeSettings = new("ManageClaudeSettings", "Manage Claude Settings");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageClaudeSettings,
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
