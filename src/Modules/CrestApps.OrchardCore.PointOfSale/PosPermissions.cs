using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PointOfSale;

public static class PosPermissions
{
    /// <summary>
    /// Rings up sales and returns, and opens and closes shifts, at the registers a staff member's roles may use.
    /// </summary>
    public static readonly Permission SellAtRegister = new("SellAtRegister", "Sell at a register");

    /// <summary>
    /// Adds and changes registers, and reads every shift.
    /// </summary>
    public static readonly Permission ManageRegisters = new("ManageRegisters", "Manage registers and shifts", [SellAtRegister]);
}

internal sealed class PosPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        PosPermissions.SellAtRegister,
        PosPermissions.ManageRegisters,
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
