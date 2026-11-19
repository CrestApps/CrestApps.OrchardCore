using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Fulfillment;

/// <summary>
/// Provides the Fulfillment Partners permissions.
/// </summary>
internal sealed class FulfillmentPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        FulfillmentPermissions.ManageFulfillmentPartners,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

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
}
