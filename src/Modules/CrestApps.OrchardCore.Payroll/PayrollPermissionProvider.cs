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

/// <summary>
/// The Payroll feature's permissions, and each employer's own copy of the per-employer ones, so a role can be given
/// "Prepare pay runs for Contoso" and nothing of another employer. The Staff role gets the two "own" permissions.
/// </summary>
internal sealed class PayrollPermissionProvider : IPermissionProvider
{
    // Export payroll data is Pay Setup's, which the Payroll feature depends on; its per-employer copies are listed here.
    private static readonly Permission[] _own = [.. PayrollPermissions.Payroll.Where(permission => permission != PayrollPermissions.ExportPayrollData)];

    private readonly CrestApps.OrchardCore.Payroll.Core.Services.EmployerStore _employers;

    public PayrollPermissionProvider(CrestApps.OrchardCore.Payroll.Core.Services.EmployerStore employers)
    {
        _employers = employers;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_own);

        foreach (var employer in await _employers.ListAsync())
        {
            permissions.AddRange(PayrollPermissions.PerEmployer.Select(permission => PayrollPermissionTemplates.For(permission, employer.ItemId, employer.Name)));
        }

        return permissions;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _own,
            },
            new PermissionStereotype
            {
                Name = CrestApps.OrchardCore.Staff.StaffConstants.DefaultStaffRoleName,
                Permissions = [PayrollPermissions.ViewOwnPay, PayrollPermissions.ManageOwnPayDetails],
            },
        ];
}
