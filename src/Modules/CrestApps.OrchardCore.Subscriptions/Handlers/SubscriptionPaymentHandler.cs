using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using OrchardCore.Entities;
using OrchardCore.Modules;

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
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPaymentHandler"/> class.
    /// </summary>
    /// <param name="paymentSession">The payment session used to stage subscription payment metadata.</param>
    /// <param name="stripePaymentService">The Stripe payment intent service used to confirm initial payment intents.</param>
    /// <param name="subscriptionSessionStore">The store used to load and save subscription sessions.</param>
    /// <param name="subscriptionTaxService">The tax service used to capture recurring payment tax snapshots.</param>
    /// <param name="clock">The clock used to date recorded payments and advance renewal expirations.</param>
    public SubscriptionPaymentHandler(
        SubscriptionPaymentSession paymentSession,
        IStripePaymentIntentService stripePaymentService,
        ISubscriptionSessionStore subscriptionSessionStore,
        ISubscriptionTaxService subscriptionTaxService,
        IClock clock
        )
    {
        _paymentSession = paymentSession;
        _stripePaymentService = stripePaymentService;
        _subscriptionSessionStore = subscriptionSessionStore;
        _subscriptionTaxService = subscriptionTaxService;
        _clock = clock;
    }

    /// <summary>
    /// Records metadata for a succeeded payment intent associated with a subscription session.
    /// </summary>
    /// <param name="context">The payment intent success context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public override Task PaymentIntentSucceededAsync(PaymentIntentSucceededContext context, CancellationToken cancellationToken = default)
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
            CreatedUtc = _clock.UtcNow,
        });
    }

    /// <summary>
    /// Records succeeded subscription creation, renewal, and update payments on the related subscription session.
    /// </summary>
    /// <param name="context">The payment success context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public override async Task PaymentSucceededAsync(PaymentSucceededContext context, CancellationToken cancellationToken = default)
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

            var now = _clock.UtcNow;

            var payment = new PaymentInfo()
            {
                TransactionId = context.TransactionId,
                Amount = context.AmountPaid,
                Currency = context.Currency,
                SubscriptionId = subscriptionId,
                GatewayId = context.GatewayId,
                GatewayMode = context.GatewayMode,
                Status = PaymentStatus.Succeeded,
                CreatedUtc = now,
            };

            // Redetermine tax for this billing cycle with the rules effective now and capture an
            // immutable snapshot on this payment. Prior payments keep their own historical snapshots.
            await _subscriptionTaxService.ApplyRecurringTaxAsync(payment, session, cancellationToken);

            session.Alter<PaymentsMetadata>(metadata =>
            {
                metadata.Payments.TryAdd(context.TransactionId, payment);
            });

            // A renewal buys another billing cycle, so the subscription's expiration has to move forward.
            // Without this the subscription looks like it expires at the end of its first cycle no matter how
            // long the customer keeps paying, which drives both the expiring-subscriptions report and any
            // access check off the wrong date.
            AdvanceSubscriptionExpiration(session, subscriptionId, now);

            await _subscriptionSessionStore.SaveAsync(session);
        }
    }

    // Advances the stored expiration of the renewed subscription by exactly one billing cycle. The cycle length
    // comes from the recorded line items, and the new expiration is measured from the previous expiration (not
    // from "now") so a renewal processed late does not shorten the customer's paid period. A subscription whose
    // expiration is already in the future by more than one cycle is left alone, which makes a replayed webhook
    // that slipped past the transaction-id guard harmless.
    private static void AdvanceSubscriptionExpiration(SubscriptionSession session, string subscriptionId, DateTime now)
    {
        if (!session.TryGet<SubscriptionsMetadata>(out var metadata) || metadata.Subscriptions is null)
        {
            return;
        }

        var subscription = metadata.Subscriptions
            .FirstOrDefault(x => string.Equals(x.SubscriptionId, subscriptionId, StringComparison.Ordinal));

        if (subscription?.LineItems is null)
        {
            return;
        }

        var plan = subscription.LineItems
            .Select(lineItem => lineItem.Subscription)
            .FirstOrDefault(x => x is not null && x.BillingDuration > 0);

        if (plan is null)
        {
            return;
        }

        var from = subscription.ExpiresAt ?? now;

        subscription.ExpiresAt = BillingSchedule.GetNextBillingDate(from, plan.DurationType, plan.BillingDuration);

        session.Put(metadata);
    }

    /// <summary>
    /// Records that a renewal failed at the gateway by moving the affected subscription to a past-due state, so
    /// the site can chase or restrict the customer instead of only noticing that payments stopped arriving.
    /// </summary>
    /// <param name="context">The failed cycle payment context.</param>
    public override async Task SubscriptionPaymentFailedAsync(SubscriptionPaymentFailedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var session = await ResolveSessionAsync(context.Data);

        if (session is null)
        {
            return;
        }

        var now = _clock.UtcNow;

        var updated = UpdateSubscription(session, context.SubscriptionId, subscription =>
        {
            // A cancellation is a later, stronger state than a failed payment, so a late-arriving failure for an
            // already-canceled subscription must not resurrect it as past due.
            if (subscription.Status is SubscriptionLifecycleStatus.Canceled or SubscriptionLifecycleStatus.Expired)
            {
                return false;
            }

            subscription.Status = SubscriptionLifecycleStatus.PastDue;

            // Keep the first failure date across the gateway's retry sequence so a dunning window is measured
            // from when the subscription actually fell behind, not from the latest retry.
            subscription.PastDueSinceUtc ??= now;

            return true;
        });

        if (updated)
        {
            await _subscriptionSessionStore.SaveAsync(session);
        }
    }

    /// <summary>
    /// Applies a subscription state change reported by the gateway, including a cancellation made outside the
    /// application.
    /// </summary>
    /// <param name="context">The subscription status change context.</param>
    public override async Task SubscriptionStatusChangedAsync(SubscriptionStatusChangedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Status == RemoteSubscriptionStatus.Unknown)
        {
            // The adapter did not recognize the gateway's state. Leave local state alone rather than guessing.
            return;
        }

        var session = await ResolveSessionAsync(context.Data);

        if (session is null)
        {
            return;
        }

        var updated = UpdateSubscription(session, context.SubscriptionId, subscription =>
        {
            var status = MapStatus(context.Status);

            // An ended subscription is terminal. A stale or out-of-order event must never move it back to an
            // active state, which would restore access the customer no longer paid for.
            if (subscription.Status == SubscriptionLifecycleStatus.Expired && status != SubscriptionLifecycleStatus.Expired)
            {
                return false;
            }

            subscription.Status = status;
            subscription.CancelAtPeriodEnd = context.CancelAtPeriodEnd;

            if (context.CanceledUtc.HasValue)
            {
                subscription.CanceledAt = context.CanceledUtc;
            }

            // The gateway is authoritative for the paid-through date, so adopt it when it reports one. This is
            // what lets a subscription canceled at period end keep access until the period actually ends.
            if (context.CurrentPeriodEndUtc.HasValue)
            {
                subscription.ExpiresAt = context.CurrentPeriodEndUtc;
            }

            if (status == SubscriptionLifecycleStatus.Active)
            {
                subscription.PastDueSinceUtc = null;
            }

            return true;
        });

        if (updated)
        {
            await _subscriptionSessionStore.SaveAsync(session);
        }
    }

    private static SubscriptionLifecycleStatus MapStatus(RemoteSubscriptionStatus status)
        => status switch
        {
            RemoteSubscriptionStatus.Trialing => SubscriptionLifecycleStatus.Trialing,
            RemoteSubscriptionStatus.Active => SubscriptionLifecycleStatus.Active,
            RemoteSubscriptionStatus.PastDue or RemoteSubscriptionStatus.Unpaid => SubscriptionLifecycleStatus.PastDue,
            RemoteSubscriptionStatus.Canceled or RemoteSubscriptionStatus.IncompleteExpired => SubscriptionLifecycleStatus.Canceled,
            RemoteSubscriptionStatus.Paused => SubscriptionLifecycleStatus.Paused,

            // Incomplete means the first payment has not confirmed yet, which the checkout flow itself is
            // already tracking; it is not a change to a purchased subscription.
            _ => SubscriptionLifecycleStatus.Active,
        };

    // Resolves the owning session from the gateway metadata. Every subscription this module creates carries the
    // originating session id in its provider metadata, which is the only correlation available until a
    // subscription is a first-class record indexed by its provider id.
    private async Task<SubscriptionSession> ResolveSessionAsync(Dictionary<string, object> data)
    {
        if (data is null || !data.TryGetValue("sessionId", out var sessionId) || sessionId is null)
        {
            return null;
        }

        var value = sessionId.ToString();

        return string.IsNullOrEmpty(value)
            ? null
            : await _subscriptionSessionStore.GetAsync(value);
    }

    // Applies a mutation to one recorded subscription and reports whether anything changed, so the caller only
    // writes the session when there is something to persist.
    private static bool UpdateSubscription(SubscriptionSession session, string subscriptionId, Func<SubscriptionInfo, bool> mutate)
    {
        if (string.IsNullOrEmpty(subscriptionId) ||
            !session.TryGet<SubscriptionsMetadata>(out var metadata) ||
            metadata.Subscriptions is null)
        {
            return false;
        }

        var subscription = metadata.Subscriptions
            .FirstOrDefault(x => string.Equals(x.SubscriptionId, subscriptionId, StringComparison.Ordinal));

        if (subscription is null || !mutate(subscription))
        {
            return false;
        }

        session.Put(metadata);

        return true;
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
            CreatedUtc = _clock.UtcNow,
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
