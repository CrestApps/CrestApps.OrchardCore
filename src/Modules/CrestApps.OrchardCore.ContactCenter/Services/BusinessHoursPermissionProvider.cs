using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the business-hours management permission, so the Business Hours feature can be administered on its own —
/// including when only automated Omnichannel conversations pulled it in — without the Work Distribution feature that
/// otherwise exposes it.
/// </summary>
internal sealed class BusinessHoursPermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        ContactCenterPermissions.ManageBusinessHours,
    ];

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
