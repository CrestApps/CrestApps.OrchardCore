using CrestApps.OrchardCore.Commerce;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reviews;

internal sealed class ReviewsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions = [ReviewsPermissions.ManageReviews, ReviewsPermissions.SubmitReviews];

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
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions = [ReviewsPermissions.SubmitReviews],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions = [ReviewsPermissions.ManageReviews],
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.Marketing,
                Permissions = [ReviewsPermissions.ManageReviews],
            },
        ];
}
