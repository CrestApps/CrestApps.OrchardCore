using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Provides the permissions exposed by the Telephony feature.
/// </summary>
internal sealed class TelephonyPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        TelephonyPermissions.ManageTelephonySettings,
        TelephonyPermissions.UseSoftPhone,
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
        ];
    }
}
