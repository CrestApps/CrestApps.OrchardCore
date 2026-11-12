using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.PointOfSale;

public static class PosPermissions
{
    /// <summary>
    /// Adds and changes registers, and reads every shift.
    /// </summary>
    public static readonly Permission ManageRegisters = new(
        "ManageRegisters",
        LocalizationSource.Create("Manage registers and shifts", typeof(PosPermissions)));

    /// <summary>
    /// Rings up sales and returns, and opens and closes shifts, at the registers a staff member's roles may use.
    /// Whoever manages registers may sell at them.
    /// </summary>
    public static readonly Permission SellAtRegister = new(
        "SellAtRegister",
        LocalizationSource.Create("Sell at a register", typeof(PosPermissions)),
        [ManageRegisters]);

    /// <summary>
    /// Refunds sales at the till and pays cash back out of the drawer, for sales of the register's own store (or any
    /// order, for staff who also manage orders). Whoever manages registers may refund at them.
    /// </summary>
    public static readonly Permission RefundAtRegister = new(
        "RefundAtRegister",
        LocalizationSource.Create("Refund at a register", typeof(PosPermissions)),
        [ManageRegisters]);
}
