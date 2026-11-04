using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Accounts.B2B;

internal sealed class B2BAccountsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        B2BAccountsPermissions.ManageB2BAccounts,
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
