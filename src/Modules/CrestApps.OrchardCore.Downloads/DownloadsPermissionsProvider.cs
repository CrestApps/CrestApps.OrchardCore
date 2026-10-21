using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Downloads;

internal sealed class DownloadsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions = [DownloadsPermissions.ManageDownloads];

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
