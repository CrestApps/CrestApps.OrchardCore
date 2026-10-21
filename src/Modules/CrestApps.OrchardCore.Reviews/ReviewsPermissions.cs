using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reviews;

public static class ReviewsPermissions
{
    public static readonly Permission ManageReviews = new("ManageProductReviews", "Moderate product reviews");

    public static readonly Permission SubmitReviews = new("SubmitProductReviews", "Review products bought");
}

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
        ];
}
