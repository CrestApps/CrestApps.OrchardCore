using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Orders;

public static class OrderPermissions
{
    public static readonly Permission ManageOrders = new(
        "ManageOrders",
        LocalizationSource.Create("Manage orders", typeof(OrderPermissions)),
        isSecurityCritical: true);

    public static readonly Permission ViewOwnOrders = new(
        "ViewOwnOrders",
        LocalizationSource.Create("View own orders", typeof(OrderPermissions)));

    public static readonly Permission ManageGiftCards = new(
        "ManageGiftCards",
        LocalizationSource.Create("Manage gift cards", typeof(OrderPermissions)),
        isSecurityCritical: true);
}
