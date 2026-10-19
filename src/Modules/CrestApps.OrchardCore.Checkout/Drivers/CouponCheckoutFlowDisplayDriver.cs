using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Checkout.Drivers;

/// <summary>
/// Renders the coupon box on the payment step and validates the code the customer enters.
/// </summary>
/// <remarks>
/// The code is checked the moment it is entered, so a customer is told "that code has expired" while they
/// can still do something about it, rather than watching a total that never changes and guessing why.
///
/// An invalid code is rejected without blocking the purchase: the checkout proceeds at full price. Refusing
/// to let somebody buy because their promotional code is wrong loses a sale to protect a discount.
/// </remarks>
public sealed class CouponCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    private readonly ICouponStore _couponStore;
    private readonly CouponScopeEvaluator _scopeEvaluator;
    private readonly ICouponRedemptionService _redemptions;
    private readonly IPaymentAttemptLimiter _limiter;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponCheckoutFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="couponStore">The coupon catalog.</param>
    /// <param name="scopeEvaluator">The rule that limits a coupon to some sources, reference types or one customer.</param>
    /// <param name="redemptions">The coupon uses, to refuse a code that is used up or that this customer has used enough.</param>
    /// <param name="limiter">The limiter that throttles how fast codes can be tried.</param>
    /// <param name="httpContextAccessor">The accessor of the current request, for the address codes are tried from.</param>
    /// <param name="invoiceBuilder">The builder that rebuilds the invoice once a code is applied or removed.</param>
    /// <param name="clock">The clock used to check the validity window.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CouponCheckoutFlowDisplayDriver(
        ICouponStore couponStore,
        CouponScopeEvaluator scopeEvaluator,
        ICouponRedemptionService redemptions,
        IPaymentAttemptLimiter limiter,
        IHttpContextAccessor httpContextAccessor,
        CheckoutInvoiceBuilder invoiceBuilder,
        IClock clock,
        IStringLocalizer<CouponCheckoutFlowDisplayDriver> stringLocalizer)
    {
        _couponStore = couponStore;
        _scopeEvaluator = scopeEvaluator;
        _redemptions = redemptions;
        _limiter = limiter;
        _httpContextAccessor = httpContextAccessor;
        _invoiceBuilder = invoiceBuilder;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string StepKey
        => CheckoutConstants.PaymentStepKey;

    /// <inheritdoc/>
    protected override Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (RefusesDiscounts(flow))
        {
            // Paying off a balance that already exists is not a purchase, so there is no price for a code to
            // reduce unless the module settling the balance takes the discount off it. Offering the box otherwise
            // would let a customer redeem a code against a total that does not change, leaving the balance partly
            // unpaid.
            return Task.FromResult<IDisplayResult>(null);
        }

        var codes = flow.Session.TryGet<CheckoutCoupon>(out var coupon) ? coupon.GetCodes() : [];

        return Task.FromResult<IDisplayResult>(Initialize<CouponViewModel>("CheckoutCoupon", model =>
        {
            // The invoice is what actually reduced, so it is what the box reports. Saying a code is applied
            // because it was accepted, when it took nothing off, would be a lie the customer discovers at
            // the total; a code that gave way to a discount it can't be combined with says so.
            var invoice = flow.Session.TryGet<CheckoutInvoice>(out var built) ? built : null;

            model.AppliedAmount = invoice?.DiscountTotal ?? 0m;
            model.Currency = invoice?.Currency;
            model.CanAddMore = codes.Count < CheckoutCoupon.MaxCodes;
            model.Applied = [.. codes.Select(code =>
            {
                var discount = invoice?.Discounts?.FirstOrDefault(entry => Matches(entry, code));
                var skipped = discount is null ? invoice?.SkippedDiscounts?.FirstOrDefault(entry => Matches(entry, code)) : null;

                return new AppliedCouponViewModel
                {
                    Code = code,
                    Description = discount?.Description ?? skipped?.Description,
                    Amount = discount?.Amount ?? 0m,
                    GaveWayTo = skipped?.GaveWayTo,
                };
            })];
        }).Location("Aside:5"));
    }

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (RefusesDiscounts(flow))
        {
            // A code posted to a settlement checkout that takes no discounts (a stale form, or a hand-built
            // request) is ignored rather than stored, so it can never reach the invoice or be redeemed.
            return null;
        }

        var model = new CouponViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var applied = flow.Session.TryGet<CheckoutCoupon>(out var existing) ? existing : new CheckoutCoupon();
        var action = _httpContextAccessor.HttpContext?.Request.HasFormContentType == true
            ? _httpContextAccessor.HttpContext.Request.Form[CheckoutConstants.FormActions.FieldName].ToString()
            : null;

        // Each code has its own Remove button, which is how a customer takes one off.
        if (action?.StartsWith(CheckoutConstants.FormActions.RemoveCouponPrefix, StringComparison.Ordinal) == true)
        {
            if (applied.Remove(action[CheckoutConstants.FormActions.RemoveCouponPrefix.Length..]))
            {
                flow.Session.Put(applied);
                await _invoiceBuilder.BuildAsync(flow);
            }

            return await EditStepAsync(flow, context);
        }

        var code = model.Code?.Trim();

        if (string.IsNullOrEmpty(code))
        {
            return await EditStepAsync(flow, context);
        }

        // Codes are short enough to guess, so how fast they can be tried from one address is limited.
        var address = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

        if (!await _limiter.AcquireAsync("coupon-code", address))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), S["Too many codes were tried. Wait a few minutes, then try again."]);

            return await EditStepAsync(flow, context);
        }

        var coupon = await _couponStore.GetByCodeAsync(code);

        // An automatic discount's code is never typed; it applies by itself when the order qualifies. A coupon that
        // only takes the codes generated for it doesn't take its own.
        if (coupon is { CanBeTyped: false })
        {
            coupon = null;
        }
        var availability = coupon is null || !coupon.IsRedeemable(_clock.UtcNow)
            ? CouponAvailability.Unavailable
            : await _redemptions.GetAvailabilityAsync(coupon, flow);

        if (coupon is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), S["That code is not recognized."]);
        }
        else if (!await _scopeEvaluator.IsInScopeAsync(coupon, flow))
        {
            // A coupon limited to some sources, kinds of charge or one customer is refused everywhere else, here
            // where the customer can still do something about it, and again by the discount provider.
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), S["That code can't be used for this payment."]);
        }
        else if (availability == CouponAvailability.CustomerLimitReached)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), S["You have already used that code as many times as it allows."]);
        }
        else if (availability != CouponAvailability.Available)
        {
            // Used up counts the uses held by checkouts that are paying right now: if one of them is let go, the code
            // works again.
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), S["That code is no longer available."]);
        }
        else if (!applied.Add(coupon.Code))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), S["A checkout takes up to {0} codes. Remove one to add another.", CheckoutCoupon.MaxCodes]);
        }
        else
        {
            flow.Session.Put(applied);

            // The invoice is rebuilt here rather than at completion, so the customer sees what the code
            // actually took off before they agree to pay. A discount the customer cannot see until after
            // the charge is a discount they cannot check.
            await _invoiceBuilder.BuildAsync(flow);
        }

        return await EditStepAsync(flow, context);
    }

    private static bool Matches(DiscountLine discount, string code)
        => string.Equals(discount.ProviderKey, Core.Services.CouponDiscountProvider.ProviderKey, StringComparison.Ordinal) &&
            string.Equals(discount.Code, code, StringComparison.OrdinalIgnoreCase);

    private static bool RefusesDiscounts(CheckoutFlow flow)
        => flow.Session is CheckoutSession { SettlesExistingBalance: true, AcceptsDiscountsOnBalance: false };
}
