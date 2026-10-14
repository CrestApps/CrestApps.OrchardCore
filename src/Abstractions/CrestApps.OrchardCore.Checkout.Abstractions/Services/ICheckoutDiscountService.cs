using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Applies whatever reduces what a customer pays: a coupon, a promotion, a negotiated rate.
/// </summary>
/// <remarks>
/// This is a seam rather than a feature so the checkout can be built without knowing what a discount is. A
/// site with no discounting registers nothing and the invoice is unchanged; a site with coupons registers a
/// provider and nothing about the checkout changes to accommodate it.
///
/// Discounts are applied <em>before</em> tax, always. Taxing the full price and then discounting the total
/// charges the customer tax on money they never paid, which is both wrong for them and wrong on the return
/// the site owner files.
/// </remarks>
public interface ICheckoutDiscountService
{
    /// <summary>
    /// Reduces the invoice by whatever the session qualifies for, and records what was applied.
    /// </summary>
    /// <param name="invoice">The invoice to reduce. It has not been taxed yet.</param>
    /// <param name="flow">The checkout the invoice belongs to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ApplyDiscountsAsync(CheckoutInvoice invoice, CheckoutFlow flow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contributes the discounts a checkout qualifies for.
/// </summary>
/// <remarks>
/// A provider only decides <em>what</em> to take off. Applying it to the invoice, keeping the total from
/// going below zero, and rounding at the currency's own precision are the checkout's job, so no two
/// providers can disagree about the arithmetic.
/// </remarks>
public interface ICheckoutDiscountProvider
{
    /// <summary>
    /// The stable key of the provider, recorded on each discount it contributes.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Returns the discounts this checkout qualifies for.
    /// </summary>
    /// <param name="context">The invoice and checkout being discounted.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<DiscountLine>> GetDiscountsAsync(CheckoutDiscountContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the discounts were actually used, once the checkout completes.
    /// </summary>
    /// <remarks>
    /// It is separate from computing them because a checkout that is abandoned must not consume a
    /// single-use coupon. Only a completed purchase does.
    /// </remarks>
    /// <param name="context">The completed checkout and the discounts it used.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RedeemAsync(CheckoutDiscountRedemptionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The invoice and checkout a provider is asked to discount.
/// </summary>
/// <param name="invoice">The untaxed invoice.</param>
/// <param name="flow">The checkout the invoice belongs to.</param>
public sealed class CheckoutDiscountContext(CheckoutInvoice invoice, CheckoutFlow flow)
{
    /// <summary>
    /// Gets the invoice being discounted. It has not been taxed yet.
    /// </summary>
    public CheckoutInvoice Invoice { get; } = invoice;

    /// <summary>
    /// Gets the checkout the invoice belongs to.
    /// </summary>
    public CheckoutFlow Flow { get; } = flow;
}

/// <summary>
/// The completed checkout and the discounts it used.
/// </summary>
/// <param name="flow">The completed checkout.</param>
/// <param name="discounts">The discounts recorded on its invoice.</param>
public sealed class CheckoutDiscountRedemptionContext(CheckoutFlow flow, IReadOnlyList<DiscountLine> discounts)
{
    /// <summary>
    /// Gets the completed checkout.
    /// </summary>
    public CheckoutFlow Flow { get; } = flow;

    /// <summary>
    /// Gets the discounts recorded on its invoice.
    /// </summary>
    public IReadOnlyList<DiscountLine> Discounts { get; } = discounts;
}
