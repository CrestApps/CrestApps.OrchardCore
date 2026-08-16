using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions.Handlers;

/// <summary>
/// Handles payment provider events that update subscription session payment metadata.
/// </summary>
public sealed class SubscriptionPaymentHandler : PaymentEventBase
{
    private readonly SubscriptionPaymentSession _paymentSession;
    private readonly IStripePaymentIntentService _stripePaymentService;
    private readonly ISubscriptionSessionStore _subscriptionSessionStore;
    private readonly ISubscriptionTaxService _subscriptionTaxService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPaymentHandler"/> class.
    /// </summary>
    /// <param name="paymentSession">The payment session used to stage subscription payment metadata.</param>
    /// <param name="stripePaymentService">The Stripe payment intent service used to confirm initial payment intents.</param>
    /// <param name="subscriptionSessionStore">The store used to load and save subscription sessions.</param>
    /// <param name="subscriptionTaxService">The tax service used to capture recurring payment tax snapshots.</param>
    public SubscriptionPaymentHandler(
        SubscriptionPaymentSession paymentSession,
        IStripePaymentIntentService stripePaymentService,
        ISubscriptionSessionStore subscriptionSessionStore,
        ISubscriptionTaxService subscriptionTaxService
        )
    {
        _paymentSession = paymentSession;
        _stripePaymentService = stripePaymentService;
        _subscriptionSessionStore = subscriptionSessionStore;
        _subscriptionTaxService = subscriptionTaxService;
    }

    /// <summary>
    /// Records metadata for a succeeded payment intent associated with a subscription session.
    /// </summary>
    /// <param name="context">The payment intent success context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Records succeeded subscription creation, renewal, and update payments on the related subscription session.
    /// </summary>
    /// <param name="context">The payment success context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public override async Task PaymentSucceededAsync(PaymentSucceededContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Record the initial subscription payment as well as recurring renewal ('cycle') and update
        // payments. Only unrelated reasons (e.g. one-off manual charges) are ignored here.
        if (context.Reason != PaymentReason.SubscriptionCreate &&
            context.Reason != PaymentReason.SubscriptionCycle &&
            context.Reason != PaymentReason.SubscriptionUpdate)
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
            // Save additional (renewal/update) payments. Provider webhooks are delivered at-least-once,
            // so keep this idempotent by keying on the transaction id. Skip repeat deliveries before
            // doing any tax work so a duplicate never recomputes or overwrites an existing snapshot.
            if (session.TryGet<PaymentsMetadata>(out var existing) &&
                existing.Payments is not null &&
                existing.Payments.ContainsKey(context.TransactionId))
            {
                return;
            }

            var payment = new PaymentInfo()
            {
                TransactionId = context.TransactionId,
                Amount = context.AmountPaid,
                Currency = context.Currency,
                SubscriptionId = subscriptionId,
                GatewayId = context.GatewayId,
                GatewayMode = context.GatewayMode,
                Status = PaymentStatus.Succeeded,
            };

            // Redetermine tax for this billing cycle with the rules effective now and capture an
            // immutable snapshot on this payment. Prior payments keep their own historical snapshots.
            await _subscriptionTaxService.ApplyRecurringTaxAsync(payment, session);

            session.Alter<PaymentsMetadata>(metadata =>
            {
                metadata.Payments.TryAdd(context.TransactionId, payment);
            });

            await _subscriptionSessionStore.SaveAsync(session);
        }
    }

    private async Task ProcessFirstPaymentAsync(PaymentSucceededContext context, object sessionId, SubscriptionSession session, string subscriptionId)
    {
        var payment = new PaymentInfo
        {
            TransactionId = context.TransactionId,
            SubscriptionId = subscriptionId,
            Currency = context.Currency,
            Amount = context.AmountPaid,
            GatewayMode = context.GatewayMode,
            GatewayId = context.GatewayId,
            Status = PaymentStatus.Succeeded,
        };

        var newValue = new SubscriptionPaymentsMetadata
        {
            Payments = new Dictionary<string, PaymentInfo>
            {
                [subscriptionId] = payment,
            },
        };

        var updatedValue = await _paymentSession.AddOrUpdateAsync(sessionId.ToString(), newValue, (existingValue) =>
        {
            existingValue.Payments ??= [];

            // Payment provider webhooks (e.g. Stripe) are delivered at-least-once, so the same
            // 'subscription_create' payment can be received more than once. Keying by the subscription
            // id and overwriting with the fully-populated payment keeps this idempotent: repeated
            // deliveries neither double-count the amount nor drop fields such as the transaction id and
            // 'Succeeded' status that later validation and reconciliation rely on.
            existingValue.Payments[subscriptionId] = payment;
        });

        var stripeMetadata = session.GetOrCreate<StripeMetadata>();

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
        // The key is bound to the payment intent and method so a duplicate confirmation (e.g. a
        // replayed webhook or retried request) resolves to the original result instead of a second call.
        await _stripePaymentService.ConfirmAsync(new ConfirmPaymentIntentRequest
        {
            PaymentIntentId = stripeMetadata.PaymentIntentId,
            PaymentMethodId = stripeMetadata.PaymentMethodId,
            IdempotencyKey = StripeIdempotencyKey.Compute(
                "sub_pi_confirm",
                stripeMetadata.PaymentIntentId,
                stripeMetadata.PaymentMethodId),
        });
    }
}
