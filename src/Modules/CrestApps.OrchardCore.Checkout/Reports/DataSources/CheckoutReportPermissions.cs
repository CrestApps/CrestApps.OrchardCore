using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Checkout.Reports.DataSources;

/// <summary>
/// The permissions the checkout data sets need that Checkout does not define itself.
/// </summary>
internal static class CheckoutReportPermissions
{
    /// <summary>
    /// The Orders module's <c>ManageOrders</c> permission, which the built-in checkout report and the order screens
    /// need, so a person reads the checkouts buyers started only when they may read the orders those checkouts make.
    /// </summary>
    /// <remarks>
    /// Checkout does not reference the Orders module, so the permission is named here. Permissions are checked by name,
    /// so it is the same permission; Checkout never registers it, so it shows in the roles editor only while the Orders
    /// feature is enabled.
    /// </remarks>
    public static readonly Permission ManageOrders = new(
        "ManageOrders",
        LocalizationSource.Create("Manage orders", typeof(CheckoutReportPermissions)),
        isSecurityCritical: true);
}
