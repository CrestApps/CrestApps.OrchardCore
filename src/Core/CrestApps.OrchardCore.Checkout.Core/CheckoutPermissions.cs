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
    public static readonly Permission ManageCoupons = new("ManageCoupons", "Manage coupons");
}
