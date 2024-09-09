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

        var subscriptionId = context.Subscription?.SubscriptionId ?? string.Empty;

        var newValue = new SubscriptionPaymentsMetadata
        {
            Payments = [],
        };

        newValue.Payments[subscriptionId] = new SubscriptionPaymentMetadata
        {
            Currency = context.Currency,
            Amount = context.AmountPaid,
            GatewayMode = context.Mode,
            GatewayId = context.Gateway,
        };

        var updatedValue = await _paymentSession.AddOrUpdateAsync(sessionId.ToString(), newValue, (existingValue) =>
        {
            existingValue.Payments.TryGetValue(subscriptionId, out var payment);

            existingValue.Payments[subscriptionId] = new SubscriptionPaymentMetadata
            {
                Amount = (payment?.Amount ?? 0) + context.AmountPaid,
            };
        });

        if (string.IsNullOrEmpty(stripeMetadata.PaymentIntentId))
        {
            return;
        }

        if (stripeMetadata.Subscriptions == null ||
            updatedValue.Payments.Keys.Count != stripeMetadata.Subscriptions.Count ||
            updatedValue.Payments.Keys.Count != updatedValue.Payments.Keys.Union(stripeMetadata.Subscriptions.Keys).Count())
        {
            return;
        }

        // When this succeed, the webhook will trigger the 'PaymentIntentSucceededAsync' event.
        await _stripePaymentService.ConfirmAsync(new ConfirmPaymentIntentRequest
        {
            PaymentIntentId = stripeMetadata.PaymentIntentId,
            PaymentMethodId = stripeMetadata.PaymentMethodId,
        });
    }
}
