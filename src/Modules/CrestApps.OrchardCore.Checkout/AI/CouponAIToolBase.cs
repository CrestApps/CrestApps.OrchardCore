using System.Globalization;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Checkout.AI;

/// <summary>
/// A coupon AI tool. Every coupon tool needs <see cref="CheckoutPermissions.ManageCoupons"/>, as Commerce › Coupons. A
/// coupon is found by its id or its own code; a generated code is never looked up, listed or returned, because each one
/// is a discount anyone holding it can spend.
/// </summary>
internal abstract class CouponAIToolBase : CheckoutAIToolBase
{
    /// <inheritdoc/>
    protected override Permission Permission => CheckoutPermissions.ManageCoupons;

    /// <summary>
    /// Finds a coupon by its id, or by its own code.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="couponId">The coupon's id, or <see langword="null"/>.</param>
    /// <param name="code">The coupon's code, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The coupon, or the sentence that says it was not found.</returns>
    protected static async Task<(Coupon Coupon, string Error)> FindCouponAsync(IServiceProvider services, string couponId, string code, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<ICouponStore>();

        if (couponId is not null)
        {
            return await store.FindByIdAsync(couponId, cancellationToken) is { } byId
                ? (byId, null)
                : (null, $"Nothing was done: no coupon has the id '{couponId}'. Use listCoupons to find it.");
        }

        if (code is null)
        {
            return (null, "Nothing was done: give the coupon's id or its code. Use listCoupons to find it.");
        }

        var coupon = await store.GetByCodeAsync(code, cancellationToken);

        // A generated code is looked up as its coupon; the tools work on the coupon itself, found by its own code.
        return coupon is null || coupon.GeneratedCodeId is not null
            ? (null, $"Nothing was done: no coupon has the code '{code}'. Use listCoupons to find it; a code generated for a coupon is not looked up here.")
            : (coupon, null);
    }

    /// <summary>
    /// Describes a coupon for a list.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>The description.</returns>
    protected static async Task<object> SummarizeAsync(IServiceProvider services, Coupon coupon, DateTime utcNow)
        => new
        {
            couponId = coupon.ItemId,
            code = coupon.Code,
            method = coupon.IsAutomatic ? "automatic" : "code",
            description = coupon.Description,
            takesOff = TakesOff(coupon),
            enabled = coupon.IsEnabled,
            usableNow = coupon.IsEnabled && coupon.IsRedeemable(utcNow),
            starts = await LocalTimeAsync(services, coupon.StartsUtc),
            ends = await LocalTimeAsync(services, coupon.EndsUtc),
            maxUses = coupon.MaxRedemptions,
            uses = coupon.RedemptionCount,
            usesLeft = coupon.MaxRedemptions is { } max ? Math.Max(0, max - coupon.RedemptionCount) : (int?)null,
            unbounded = IsUnbounded(coupon) ? true : (bool?)null,
        };

    /// <summary>
    /// Describes a coupon with all its rules.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns>The description.</returns>
    protected static async Task<object> DescribeAsync(IServiceProvider services, Coupon coupon, DateTime utcNow)
        => new
        {
            couponId = coupon.ItemId,
            code = coupon.Code,
            method = coupon.IsAutomatic ? "automatic" : "code",
            onlyGeneratedCodes = coupon.OnlyGeneratedCodes,
            description = coupon.Description,
            kind = ToCamel(coupon.Kind.ToString()),
            takesOff = TakesOff(coupon),
            percentage = coupon.Kind is CouponKind.Percentage or CouponKind.BuyXGetY ? coupon.Percentage : (decimal?)null,
            amount = coupon.Kind == CouponKind.FixedAmount ? coupon.Amount : (decimal?)null,
            currency = coupon.Kind == CouponKind.FixedAmount ? coupon.Currency : null,
            target = ToCamel(coupon.Target.ToString()),
            enabled = coupon.IsEnabled,
            usableNow = coupon.IsEnabled && coupon.IsRedeemable(utcNow),
            unbounded = IsUnbounded(coupon),
            starts = await LocalTimeAsync(services, coupon.StartsUtc),
            ends = await LocalTimeAsync(services, coupon.EndsUtc),
            maxUses = coupon.MaxRedemptions,
            maxUsesPerCustomer = coupon.MaxRedemptionsPerCustomer,
            uses = coupon.RedemptionCount,
            minimumAmount = coupon.MinimumAmount,
            maximumAmount = coupon.MaximumAmount,
            minimumQuantity = coupon.MinimumQuantity,
            buyQuantity = coupon.Kind == CouponKind.BuyXGetY ? coupon.BuyQuantity : (int?)null,
            getQuantity = coupon.Kind == CouponKind.BuyXGetY ? coupon.GetQuantity : (int?)null,
            maxApplicationsPerOrder = coupon.MaxApplicationsPerOrder,
            combinesWithProductDiscounts = coupon.CombinesWithProductDiscounts,
            combinesWithOrderDiscounts = coupon.CombinesWithOrderDiscounts,
            combinesWithShippingDiscounts = coupon.CombinesWithShippingDiscounts,
            productIds = coupon.ProductIds ?? [],
            categoryIds = coupon.CategoryIds ?? [],
            buyProductIds = coupon.BuyProductIds ?? [],
            buyCategoryIds = coupon.BuyCategoryIds ?? [],
            customerId = coupon.CustomerId,
            customerEmail = coupon.CustomerEmail,
            sources = coupon.Sources ?? [],
            referenceTypes = coupon.ReferenceTypes ?? [],
            created = await LocalTimeAsync(services, coupon.CreatedUtc),
        };

    /// <summary>
    /// Returns whether a coupon is enabled with neither a use limit nor an end date, so a code that leaks discounts
    /// without end. The coupon list warns about these.
    /// </summary>
    /// <param name="coupon">The coupon.</param>
    /// <returns><see langword="true"/> when it is unbounded.</returns>
    protected static bool IsUnbounded(Coupon coupon)
        => coupon.IsEnabled && !coupon.MaxRedemptions.HasValue && !coupon.EndsUtc.HasValue;

    /// <summary>
    /// Writes an enum value as the tools name it, starting in lower case.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    protected static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    // What the coupon takes off, in words a merchant reads.
    private static string TakesOff(Coupon coupon)
        => coupon.Kind switch
        {
            CouponKind.FixedAmount => $"{coupon.Amount.ToString("0.##", CultureInfo.InvariantCulture)} {coupon.Currency}",
            CouponKind.FreeShipping => "free shipping",
            CouponKind.BuyXGetY => $"buy {coupon.BuyQuantity}, get {coupon.GetQuantity} at {coupon.Percentage.ToString("0.##", CultureInfo.InvariantCulture)}% off",
            _ => $"{coupon.Percentage.ToString("0.##", CultureInfo.InvariantCulture)}%",
        };
}
