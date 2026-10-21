using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Classifieds;

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
