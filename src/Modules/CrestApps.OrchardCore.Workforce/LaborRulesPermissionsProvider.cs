using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

internal sealed class LaborRulesPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WorkforcePermissions.ManageLaborRules,
        WorkforcePermissions.OverrideLaborRules,
        WorkforcePermissions.ApprovePredictabilityPay,
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
