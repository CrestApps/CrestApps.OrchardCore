using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Provides the Timeline permissions.
/// </summary>
internal sealed class TimelinePermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        TimelinePermissions.RedactTimelineEntries,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
    }
}
