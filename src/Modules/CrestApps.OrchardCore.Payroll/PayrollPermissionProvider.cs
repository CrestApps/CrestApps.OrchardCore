using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Payroll;

internal sealed class PaySetupPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        PayrollPermissions.ManagePaySetup,
        PayrollPermissions.ExportPayrollData,
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
