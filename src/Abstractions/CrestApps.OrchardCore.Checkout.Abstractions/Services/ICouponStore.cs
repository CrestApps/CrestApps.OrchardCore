using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Persists the coupon codes a site offers.
/// </summary>
public interface ICouponStore : ICatalog<Coupon>
{
    /// <summary>
    /// Returns the coupon with the given code, matched without case sensitivity.
    /// </summary>
    /// <param name="code">The coupon code.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Coupon> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// The code a customer entered on a checkout, stored on the session so it survives the customer navigating
/// back and forth through the flow.
/// </summary>
public sealed class CheckoutCoupon
{
    /// <summary>
    /// Gets or sets the code the customer entered.
    /// </summary>
    public string Code { get; set; }
}
