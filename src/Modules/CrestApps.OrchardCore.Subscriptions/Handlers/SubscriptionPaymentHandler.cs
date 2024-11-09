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
    private readonly IStripePaymentIntentService _stripePaymentService;
    private readonly ISubscriptionSessionStore _subscriptionSessionStore;

    public SubscriptionPaymentHandler(
        SubscriptionPaymentSession paymentSession,
        IStripePaymentIntentService stripePaymentService,
        ISubscriptionSessionStore subscriptionSessionStore
        )
    {
        _paymentSession = paymentSession;
        _stripePaymentService = stripePaymentService;
        _subscriptionSessionStore = subscriptionSessionStore;
    }

    public override Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context)
    {
        if (!context.Data.TryGetValue("sessionId", out var sessionId))
        {
            return Task.CompletedTask;
        }

        return _paymentSession.SetAsync(sessionId.ToString(), new InitialPaymentMetadata
        {
            TransactionId = context.TransactionId,
            Amount = context.Amount,
            Currency = context.Currency,
            GatewayId = context.GatewayId,
            GatewayMode = context.GatewayMode,
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

        var subscriptionId = context.Subscription?.SubscriptionId ?? string.Empty;

        if (context.Reason == PaymentReason.SubscriptionCreate)
        {
            // First payment is saved to the session during the process of creating
            // a subscription to avoid concurrency issue with the current session.
            await ProcessFirstPaymentAsync(context, sessionId, session, subscriptionId);
        }
        else
        {
            // Save additional payments.
            session.Alter<PaymentsMetadata>(metadata =>
            {
                metadata.Payments.Add(context.TransactionId, new PaymentInfo()
                {
                    TransactionId = context.TransactionId,
                    Amount = context.AmountPaid,
                    Currency = context.Currency,
                    SubscriptionId = subscriptionId,
                    GatewayId = context.GatewayId,
                    GatewayMode = context.GatewayMode,
                    Status = PaymentStatus.Succeeded,
                });
            });

            await _subscriptionSessionStore.SaveAsync(session);
        }
    }

    private async Task ProcessFirstPaymentAsync(PaymentSucceededContext context, object sessionId, SubscriptionSession session, string subscriptionId)
    {
        var newValue = new SubscriptionPaymentsMetadata
        {
            Payments = [],
        };

        newValue.Payments[subscriptionId] = new PaymentInfo
        {
            TransactionId = context.TransactionId,
            SubscriptionId = subscriptionId,
            Currency = context.Currency,
            Amount = context.AmountPaid,
            GatewayMode = context.GatewayMode,
            GatewayId = context.GatewayId,
            Status = PaymentStatus.Succeeded,
        };

        var updatedValue = await _paymentSession.AddOrUpdateAsync(sessionId.ToString(), newValue, (existingValue) =>
        {
            existingValue.Payments.TryGetValue(subscriptionId, out var payment);

            existingValue.Payments[subscriptionId] = new PaymentInfo
            {
                Amount = (payment?.Amount ?? 0) + context.AmountPaid,
            };
        });

        var stripeMetadata = session.As<StripeMetadata>();

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
