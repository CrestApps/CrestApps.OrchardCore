using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// Refuses to take payment for a company buyer's order that needs a company admin's approval it doesn't have, however
/// the buyer got to paying.
/// </summary>
public sealed class CompanyApprovalPaymentGate : ICheckoutPaymentGate
{
    private readonly CompanyCheckoutStepService _step;
    private readonly IOrderStore _orders;

    internal readonly IStringLocalizer S;

    public CompanyApprovalPaymentGate(CompanyCheckoutStepService step, IOrderStore orders, IStringLocalizer<CompanyApprovalPaymentGate> stringLocalizer)
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

        context.Refuse(S["This order needs approval first: {0}. Ask for it in the Company step.", reason]);
    }
}
