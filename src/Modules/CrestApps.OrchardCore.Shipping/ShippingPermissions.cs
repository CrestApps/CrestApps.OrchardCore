using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Shipping;

public static class ShippingPermissions
{
    public static readonly Permission ManageShipping = new(
        "ManageShipping",
        LocalizationSource.Create("Manage shipping zones, methods and settings", typeof(ShippingPermissions)));

    public static readonly Permission FulfillOrders = new(
        "FulfillOrders",
        LocalizationSource.Create("Ship orders and update their shipments", typeof(ShippingPermissions)));
}
