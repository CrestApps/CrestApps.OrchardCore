using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Handlers;

public sealed class SubscriptionPaymentHandler : PaymentEventBase
{
    private readonly SubscriptionPaymentSession _paymentSession;
    private readonly IStripePaymentService _stripePaymentService;
    private readonly ISubscriptionSessionStore _subscriptionSessionStore;

    public SubscriptionPaymentHandler(
        SubscriptionPaymentSession paymentSession,
        IStripePaymentService stripePaymentService,
        ISubscriptionSessionStore subscriptionSessionStore
        )
    {
        _paymentSession = paymentSession;
        _stripePaymentService = stripePaymentService;
        _subscriptionSessionStore = subscriptionSessionStore;
    }

    public override async Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
    {
        if (!context.Data.TryGetValue("sessionId", out var sessionId))
        {
            return;
        }

        await _paymentSession.SetAsync(sessionId.ToString(), new InitialPaymentMetadata
        {
            Mode = context.Mode,
            Amount = context.AmountPaid,
            Currency = context.Currency,
        });
    }

    public override async Task PaymentSucceededAsync(PaymentSucceededContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Reason != PaymentReason.SubscriptionCreate)
        {
            return;
        }

        object sessionId;

        if (!context.Data.TryGetValue("sessionId", out sessionId))
        {
            if (context.Subscription == null || !context.Subscription.Data.TryGetValue("sessionId", out sessionId))
            {
                return;
            }
        }

        var session = await _subscriptionSessionStore.GetAsync(sessionId.ToString());

        if (session == null)
        {
            return;
        }

        var stripeMetadata = session.As<StripeMetadata>();

        if (string.IsNullOrEmpty(stripeMetadata.PaymentIntentId))
        {
            return;
        }

        await _paymentSession.SetAsync(sessionId.ToString(), new SubscriptionPaymentMetadata
        {
            Currency = context.Currency,
            Amount = context.AmountPaid,
            Mode = context.Mode,
        });

        // When this succeed, the webhook will trigger the 'PaymentIntentSucceededAsync' event.
        await _stripePaymentService.ConfirmAsync(new ConfirmPaymentIntentRequest
        {
            PaymentIntentId = stripeMetadata.PaymentIntentId,
            PaymentMethodId = stripeMetadata.PaymentMethodId,
        });
    }
}
