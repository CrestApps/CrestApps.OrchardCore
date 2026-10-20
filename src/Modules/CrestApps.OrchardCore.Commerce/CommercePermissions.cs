using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce;

public static class CommercePermissions
{
    /// <summary>
    /// Opens the store's setup guide and runs its steps.
    /// </summary>
    public static readonly Permission SetUpStore = new("SetUpStore", "Set up the store");
}

internal sealed class CommercePermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        CommercePermissions.SetUpStore,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
