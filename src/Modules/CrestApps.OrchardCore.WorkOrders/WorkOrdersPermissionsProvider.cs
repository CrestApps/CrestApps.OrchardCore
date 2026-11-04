using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// Provides the Work Orders permissions.
/// </summary>
internal sealed class WorkOrdersPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WorkOrdersPermissions.ManageWorkOrderSettings,
        WorkOrdersPermissions.ManageWorkOrders,
        WorkOrdersPermissions.ViewWorkOrders,
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
