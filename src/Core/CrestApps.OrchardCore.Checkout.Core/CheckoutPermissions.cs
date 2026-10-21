using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Checkout.Core;

/// <summary>
/// The permissions exposed by the Checkout feature.
/// </summary>
public static class CheckoutPermissions
{
    /// <summary>
    /// The permission required to create, change, and delete coupon codes.
    /// </summary>
    /// <remarks>
    /// It is its own permission because a coupon is a standing instruction to charge customers less. Someone
    /// who can edit content should not thereby be able to publish a code that discounts every sale on the
    /// site.
    /// </remarks>
    public static readonly Permission ManageCoupons = new(
        "ManageCoupons",
        LocalizationSource.Create("Manage coupons", typeof(CheckoutPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// The permission required to change how the checkout works, under Settings › Commerce › Checkout.
    /// </summary>
    public static readonly Permission ManageCheckoutSettings = new(
        "ManageCheckoutSettings",
        LocalizationSource.Create("Manage checkout settings", typeof(CheckoutPermissions)));
}
