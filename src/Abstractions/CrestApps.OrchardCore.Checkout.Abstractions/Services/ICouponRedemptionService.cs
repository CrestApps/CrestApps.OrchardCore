using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Holds, redeems and gives back the uses of coupons, so a usage limit and a limit per customer hold even when
/// buyers race for the last use.
/// </summary>
public interface ICouponRedemptionService
{
    /// <summary>
    /// Returns whether the checkout could use the coupon right now, counting the uses taken and those held by other
    /// checkouts.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <param name="flow">The checkout.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CouponAvailability> GetAvailabilityAsync(Coupon coupon, CheckoutFlow flow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Holds a use of the discount's coupon for the checkout, or extends the hold it has. Nothing is held unless it
    /// returns <see cref="CouponAvailability.Available"/>.
    /// </summary>
    /// <param name="flow">The checkout that is about to pay.</param>
    /// <param name="discount">The coupon's discount on the checkout's invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CouponAvailability> HoldAsync(CheckoutFlow flow, DiscountLine discount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the use of the discount's coupon by a completed checkout, turning its hold into a redemption. The
    /// customer has paid, so a use is recorded even when the coupon ran out meanwhile.
    /// </summary>
    /// <param name="flow">The completed checkout.</param>
    /// <param name="discount">The coupon's discount on the checkout's invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RedeemAsync(CheckoutFlow flow, DiscountLine discount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets go of the uses a checkout holds, such as when it is canceled, fails or expires.
    /// </summary>
    /// <param name="sessionId">The checkout session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReleaseAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives back the uses a completed checkout redeemed, such as when its order is canceled, so they can be used
    /// again.
    /// </summary>
    /// <param name="sessionId">The checkout session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many uses were given back.</returns>
    Task<int> ReturnAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest uses of a coupon, newest first.
    /// </summary>
    /// <param name="code">The coupon code.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<CouponRedemption>> ListAsync(string code, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns how many uses of a coupon checkouts hold right now.
    /// </summary>
    /// <param name="code">The coupon code.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<int> CountHeldAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets who used coupons on the given checkouts, keeping the uses themselves, such as when a customer's
    /// personal data is erased.
    /// </summary>
    /// <param name="sessionIds">The customer's checkout sessions.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many uses were changed.</returns>
    Task<int> ForgetCustomersAsync(IEnumerable<string> sessionIds, CancellationToken cancellationToken = default);
}
