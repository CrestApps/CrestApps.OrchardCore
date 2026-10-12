using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
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
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponCheckoutFlowDisplayDriver"/> class.
    /// </summary>
    /// <param name="couponStore">The coupon catalog.</param>
    /// <param name="clock">The clock used to check the validity window.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CouponCheckoutFlowDisplayDriver(
        ICouponStore couponStore,
        CheckoutInvoiceBuilder invoiceBuilder,
        IClock clock,
        IStringLocalizer<CouponCheckoutFlowDisplayDriver> stringLocalizer)
    {
        _couponStore = couponStore;
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
        var applied = flow.Session.TryGet<CheckoutCoupon>(out var coupon) ? coupon : null;

        return Task.FromResult<IDisplayResult>(Initialize<CouponViewModel>("CheckoutCoupon", model =>
        {
            model.Code = applied?.Code;

            // The invoice is what actually reduced, so it is what the box reports. Saying a code is applied
            // because it was accepted, when it took nothing off, would be a lie the customer discovers at
            // the total.
            model.AppliedAmount = flow.Session.TryGet<CheckoutInvoice>(out var invoice) ? invoice.DiscountTotal : 0m;
            model.Currency = invoice?.Currency;
        }).Location("Aside:5"));
    }

    /// <inheritdoc/>
    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        var model = new CouponViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var code = model.Code?.Trim();

        if (string.IsNullOrEmpty(code))
        {
            // Clearing the box removes the coupon, which is how a customer takes one off.
            if (flow.Session.Properties?.Remove(nameof(CheckoutCoupon)) == true)
            {
                await _invoiceBuilder.BuildAsync(flow);
            }

            return await EditStepAsync(flow, context);
        }

        var coupon = await _couponStore.GetByCodeAsync(code);

        if (coupon is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), S["That code is not recognized."]);
        }
        else if (!coupon.IsRedeemable(_clock.UtcNow))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Code), S["That code is no longer available."]);
        }
        else
        {
            flow.Session.Put(new CheckoutCoupon { Code = coupon.Code });

            // The invoice is rebuilt here rather than at completion, so the customer sees what the code
            // actually took off before they agree to pay. A discount the customer cannot see until after
            // the charge is a discount they cannot check.
            await _invoiceBuilder.BuildAsync(flow);
        }

        return await EditStepAsync(flow, context);
    }
}
