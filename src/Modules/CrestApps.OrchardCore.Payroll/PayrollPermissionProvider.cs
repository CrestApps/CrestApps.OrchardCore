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
/// The Payroll feature's permissions. The Staff role gets the two "own" permissions. Multiple Employers adds each
/// employer's own copy of the per-employer ones (<see cref="PayrollEmployerPermissionProvider"/>).
/// </summary>
internal sealed class PayrollPermissionProvider : IPermissionProvider
{
    // Export payroll data is Pay Setup's, which the Payroll feature depends on.
    private static readonly Permission[] _own = [.. PayrollPermissions.Payroll.Where(permission => permission != PayrollPermissions.ExportPayrollData)];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult<IEnumerable<Permission>>(_own);

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

/// <summary>
/// With Multiple Employers on: each employer's own copy of the per-employer permissions, so a role can be given "Prepare
/// pay runs for Contoso" and nothing of another employer.
/// </summary>
internal sealed class PayrollEmployerPermissionProvider : IPermissionProvider
{
    private readonly CrestApps.OrchardCore.Payroll.Core.Services.EmployerStore _employers;

    public PayrollEmployerPermissionProvider(CrestApps.OrchardCore.Payroll.Core.Services.EmployerStore employers)
    {
        _employers = employers;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>();

        foreach (var employer in await _employers.ListAsync())
        {
            permissions.AddRange(PayrollPermissions.PerEmployer.Select(permission => PayrollPermissionTemplates.For(permission, employer.ItemId, employer.Name)));
        }

        return permissions;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [];
}
