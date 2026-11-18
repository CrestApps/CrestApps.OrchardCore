using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Transactions.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Transactions.Services;

internal sealed class PaymentTermsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        TransactionsPermissions.ManagePaymentTerms,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions = _allPermissions,
            },
        ];
    }
}
