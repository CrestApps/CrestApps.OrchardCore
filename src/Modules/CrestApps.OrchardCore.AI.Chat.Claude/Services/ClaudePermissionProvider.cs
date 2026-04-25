using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Claude;

public sealed class ClaudePermissionProvider : IPermissionProvider
{
    public static readonly Permission ManageClaudeSettings = new("ManageClaudeSettings", "Manage Claude Settings");

    private readonly IEnumerable<Permission> _allPermissions =
    [
        ManageClaudeSettings,
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
