using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Banking;

internal sealed class BankingPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        BankingPermissions.ManageBankAccounts,
        BankingPermissions.ManageOwnBankAccounts,
        BankingPermissions.RevealBankAccountNumbers,
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
