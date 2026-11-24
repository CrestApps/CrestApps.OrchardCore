using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Approvals;

internal sealed class ApprovalsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ApprovalsPermissions.ManageApprovalPolicies,
        ApprovalsPermissions.ManageApprovalRequests,
        ApprovalsPermissions.ViewApprovalRequests,
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
        ];
}
