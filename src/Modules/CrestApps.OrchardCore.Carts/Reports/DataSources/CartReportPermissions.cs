using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Carts.Reports.DataSources;

/// <summary>
/// The permissions the carts data sets need. Carts has no screens of its own, so it defines none.
/// </summary>
internal static class CartReportPermissions
{
    /// <summary>
    /// The Orders module's <c>ManageOrders</c> permission, which the order screens and reports need, so a person reads
    /// the shoppers' carts and wishlists only when they may read the orders the carts become.
    /// </summary>
    /// <remarks>
    /// Carts does not reference the Orders module, so the permission is named here. Permissions are checked by name, so
    /// it is the same permission; Carts never registers it, so it shows in the roles editor only while the Orders
    /// feature is enabled.
    /// </remarks>
    public static readonly Permission ManageOrders = new(
        "ManageOrders",
        LocalizationSource.Create("Manage orders", typeof(CartReportPermissions)),
        isSecurityCritical: true);
}
