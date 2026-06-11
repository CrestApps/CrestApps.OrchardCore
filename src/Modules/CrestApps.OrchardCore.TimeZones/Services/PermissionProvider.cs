using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeZones.Services;

internal sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _permissions =
    [
        TimeZonesConstants.Permissions.ManageTimeZoneMaps,
    ];

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _permissions,
        },
    ];

    /// <summary>
    /// Retrieves the permissions asynchronously.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_permissions);
}
