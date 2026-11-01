using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.ContactCenter;

/// <summary>
/// Declares the adherence permissions.
/// </summary>
internal sealed class AdherencePermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WorkforcePermissions.ManageAdherence,
        WorkforcePermissions.ViewAdherence,
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
