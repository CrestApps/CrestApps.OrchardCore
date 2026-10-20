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
    private readonly CheckoutCouponService _coupons;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CouponCheckoutFlowDisplayDriver(
        CheckoutCouponService coupons,
        IHttpContextAccessor httpContextAccessor)
    {
        _coupons = coupons;
        _httpContextAccessor = httpContextAccessor;
    }

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

        var action = _httpContextAccessor.HttpContext?.Request.HasFormContentType == true
            ? _httpContextAccessor.HttpContext.Request.Form[CheckoutConstants.FormActions.FieldName].ToString()
            : null;

        // Each code has its own Remove button, which is how a customer takes one off.
        if (action?.StartsWith(CheckoutConstants.FormActions.RemoveCouponPrefix, StringComparison.Ordinal) == true)
        {
            await _coupons.RemoveAsync(flow, action[CheckoutConstants.FormActions.RemoveCouponPrefix.Length..]);

            return await EditStepAsync(flow, context);
        }

        if (string.IsNullOrWhiteSpace(model.Code))
        {
            return await EditStepAsync(flow, context);
        }

        var address = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

        if (await _coupons.ApplyAsync(flow, model.Code, address) is { } error)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), error);
        }

        return await EditStepAsync(flow, context);
    }

    private static bool Matches(DiscountLine discount, string code)
        => string.Equals(discount.ProviderKey, Core.Services.CouponDiscountProvider.ProviderKey, StringComparison.Ordinal) &&
            string.Equals(discount.Code, code, StringComparison.OrdinalIgnoreCase);

    private static bool RefusesDiscounts(CheckoutFlow flow)
        => CheckoutCouponService.RefusesDiscounts(flow);
}
