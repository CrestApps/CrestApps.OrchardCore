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

        object sessionId;

        if (!context.Data.TryGetValue("sessionId", out sessionId))
        {
            if (context.Subscription == null || !context.Subscription.Data.TryGetValue("sessionId", out sessionId))
            {
                return;
            }
        }

        await _paymentSession.SetAsync(sessionId.ToString(), new InitialPaymentMetadata
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

        await _paymentSession.SetAsync(sessionId.ToString(), new SubscriptionPaymentMetadata
        {
            PlanId = context.PlanId,
            Currency = context.PlanCurrency,
            Amount = context.PlanAmount,
            Mode = context.Mode,
            SubscriptionId = context.SubscriptionId,
        });
    }
}
