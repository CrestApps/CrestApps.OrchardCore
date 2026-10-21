using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Commerce;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Provides the permissions exposed by the Checkout feature.
/// </summary>
internal sealed class CheckoutPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        CheckoutPermissions.ManageCoupons,
        CheckoutPermissions.ManageCheckoutSettings,
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
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions = [CheckoutPermissions.ManageCoupons],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.Marketing,
                Permissions = [CheckoutPermissions.ManageCoupons],
            },
        ];
    }
}
