using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Classifieds;

public static class ClassifiedsConstants
{
    public const string Feature = "CrestApps.OrchardCore.Classifieds";

    /// <summary>
    /// The cart line detail naming the listing an upgrade is bought for.
    /// </summary>
    public const string ListingDetail = "classified.listing";
}

public static class ClassifiedsPermissions
{
    /// <summary>
    /// Posts listings from the site and manages one's own.
    /// </summary>
    public static readonly Permission PostClassifieds = new("PostClassifieds", "Post listings");

    /// <summary>
    /// Approves and rejects posted listings.
    /// </summary>
    public static readonly Permission ModerateClassifieds = new("ModerateClassifieds", "Moderate posted listings");
}

internal sealed class ClassifiedsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ClassifiedsPermissions.PostClassifieds,
        ClassifiedsPermissions.ModerateClassifieds,
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
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions = [ClassifiedsPermissions.PostClassifieds],
            },
        ];
}
