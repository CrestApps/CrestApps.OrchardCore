using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Refuses to take payment for an account buyer's order that needs an account admin's approval it doesn't have, however
/// the buyer got to paying.
/// </summary>
public sealed class B2BApprovalPaymentGate : ICheckoutPaymentGate
{
    private readonly B2BCheckoutStepService _step;
    private readonly IOrderStore _orders;

    internal readonly IStringLocalizer S;

    public B2BApprovalPaymentGate(B2BCheckoutStepService step, IOrderStore orders, IStringLocalizer<B2BApprovalPaymentGate> stringLocalizer)
    {
        _step = step;
        _orders = orders;
        S = stringLocalizer;
    }

    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            !session.TryGet<CheckoutInvoice>(out var invoice) ||
            await _orders.FindByIdAsync(session.ReferenceId, cancellationToken) is not { } order ||
            await _step.GetApprovalReasonAsync(session, order, invoice, cancellationToken) is not { } reason)
        {
            return;
        }

        context.Refuse(S["This order needs approval first: {0}. Ask for it in the Business account step.", reason]);
    }
}
