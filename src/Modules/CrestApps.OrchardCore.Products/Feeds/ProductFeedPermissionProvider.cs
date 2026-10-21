using CrestApps.OrchardCore.Commerce;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.Feeds;

internal sealed class ProductFeedPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _all = [ProductFeedPermissions.ManageProductFeeds];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_all);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _all,
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions = _all,
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.Marketing,
                Permissions = _all,
            },
        ];
}
