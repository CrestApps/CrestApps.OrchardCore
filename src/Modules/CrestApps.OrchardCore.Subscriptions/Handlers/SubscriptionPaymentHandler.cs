using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Handlers;

public sealed class SubscriptionPaymentHandler : PaymentEventBase
{
    private readonly SubscriptionPaymentSession _paymentSession;

    public SubscriptionPaymentHandler(SubscriptionPaymentSession paymentSession)
    {
        _paymentSession = paymentSession;
    }

    public override async Task PaymentSucceededAsync(PaymentSucceededContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Data.TryGetValue("sessionId", out var sessionId))
        {
            return;
        }

        await _paymentSession.SetAsync(sessionId.ToString(), new InitialPaymentInfo()
        {
            InitialPaymentAmount = context.AmountPaid,
            InitialPaymentCurrency = context.Currency,
        });
    }

    public override async Task CustomerSubscriptionCreatedAsync(CustomerSubscriptionCreatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Data.TryGetValue("sessionId", out var sessionId))
        {
            return;
        }

        await _paymentSession.SetAsync(sessionId.ToString(), new SubscriptionPaymentInfo()
        {
            PlanId = context.PlanId,
            Currency = context.PlanCurrency,
            Amount = context.PlanAmount,
        });
    }
}
