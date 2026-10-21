using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Transactions.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Provides the permissions exposed by the Transactions feature.
/// </summary>
internal sealed class TransactionsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        TransactionsPermissions.ManageTransactions,
        TransactionsPermissions.ManageTransactionSettings,
        TransactionsPermissions.ManageRefunds,
        TransactionsPermissions.ViewOwnTransactions,
    ];

    private readonly IEnumerable<Permission> _authenticatedPermissions =
    [
        TransactionsPermissions.ViewOwnTransactions,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc/>
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
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions = _authenticatedPermissions,
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions =
                [
                    TransactionsPermissions.ManageTransactions,
                    TransactionsPermissions.ManageRefunds,
                ],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.CustomerSupport,
                Permissions = [TransactionsPermissions.ManageRefunds],
            },
        ];
    }
}
