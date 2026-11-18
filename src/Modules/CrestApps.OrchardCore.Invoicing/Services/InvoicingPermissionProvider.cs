using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Invoicing.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Declares the invoicing permissions. Administrators and store managers get every one; signed-in users see their own
/// invoices.
/// </summary>
internal sealed class InvoicingPermissionProvider : IPermissionProvider
{
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult<IEnumerable<Permission>>(InvoicingPermissions.All);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = InvoicingPermissions.All,
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions = InvoicingPermissions.All,
            },
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions = [InvoicingPermissions.ViewOwnInvoices],
            },
        ];
    }
}
