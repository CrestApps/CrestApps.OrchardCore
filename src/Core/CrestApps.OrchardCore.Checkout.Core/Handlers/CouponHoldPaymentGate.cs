using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Handlers;

/// <summary>
/// Holds a use of each coupon on the invoice when payment begins, and refuses the payment when one has run out,
/// taking the code off so the buyer sees the new total before paying.
/// </summary>
public sealed class CouponHoldPaymentGate : ICheckoutPaymentGate
{
    private readonly ICouponRedemptionService _redemptions;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponHoldPaymentGate"/> class.
    /// </summary>
    public CouponHoldPaymentGate(
        ICouponRedemptionService redemptions,
        CheckoutInvoiceBuilder invoiceBuilder,
        IStringLocalizer<CouponHoldPaymentGate> stringLocalizer)
    {
        _redemptions = redemptions;
        _invoiceBuilder = invoiceBuilder;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var session = context.Flow.Session;

        if (!session.TryGet<CheckoutInvoice>(out var invoice) || invoice.Discounts is not { Count: > 0 })
        {
            return;
        }

        var coupons = invoice.Discounts
            .Where(discount => string.Equals(discount.ProviderKey, CouponDiscountProvider.ProviderKey, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(discount.Code) &&
                discount.Amount > 0m)
            .ToArray();

        foreach (var discount in coupons)
        {
            var availability = await _redemptions.HoldAsync(context.Flow, discount, cancellationToken);

            if (availability == CouponAvailability.Available)
            {
                continue;
            }

            if (availability == CouponAvailability.Busy)
            {
                context.Refuse(S["We couldn't reserve your promotion code just now. Please try again."]);

                return;
            }

            // The code comes off and the invoice is rebuilt without it, so what the buyer pays next is the new total.
            session.Properties?.Remove(nameof(CheckoutCoupon));

            await _invoiceBuilder.BuildAsync(context.Flow);

            context.InvoiceChanged = true;
            context.Refuse(availability switch
            {
                CouponAvailability.CustomerLimitReached => S["You have already used the code {0} as many times as it allows, so it was taken off. Check your new total, then pay again.", discount.Code],
                CouponAvailability.UsedUp => S["The code {0} ran out before you paid, so it was taken off. Check your new total, then pay again.", discount.Code],
                _ => S["The code {0} is no longer available, so it was taken off. Check your new total, then pay again.", discount.Code],
            });

            return;
        }
    }
}
