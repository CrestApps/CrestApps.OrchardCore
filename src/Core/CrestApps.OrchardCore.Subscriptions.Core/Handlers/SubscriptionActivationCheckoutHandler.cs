using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using OrchardCore.ContentManagement;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Creates the durable <see cref="Subscription"/> for every recurring obligation a completed checkout
/// settled.
/// </summary>
/// <remarks>
/// This is the seam between buying something once and owing it from now on. The checkout knows the customer
/// agreed and paid; it does not know that the agreement has to survive, renew, lapse, and be canceled. The
/// subscription created here is what every later question is asked of, and it is created only from attempts
/// the checkout actually confirmed, so an unpaid checkout never grants an active subscription.
/// </remarks>
public sealed class SubscriptionActivationCheckoutHandler : CheckoutHandlerBase
{
    private readonly IPaymentAttemptStore _attemptStore;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IContentManager _contentManager;
    private readonly IEnumerable<ISubscriptionLifecycleHandler> _lifecycleHandlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionActivationCheckoutHandler"/> class.
    /// </summary>
    /// <param name="attemptStore">The durable payment attempt ledger.</param>
    /// <param name="subscriptionManager">The subscription manager.</param>
    /// <param name="contentManager">The content manager used to read what the plan entitles a subscriber to.</param>
    /// <param name="lifecycleHandlers">The handlers notified that the agreement now exists and is current.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SubscriptionActivationCheckoutHandler(
        IPaymentAttemptStore attemptStore,
        ISubscriptionManager subscriptionManager,
        IContentManager contentManager,
        IEnumerable<ISubscriptionLifecycleHandler> lifecycleHandlers,
        IClock clock,
        ILogger<SubscriptionActivationCheckoutHandler> logger)
    {
        _attemptStore = attemptStore;
        _subscriptionManager = subscriptionManager;
        _contentManager = contentManager;
        _lifecycleHandlers = lifecycleHandlers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is not CheckoutSession session)
        {
            return;
        }

        if (!session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return;
        }

        var groups = invoice.GetRecurringGroups();

        if (groups.Count == 0)
        {
            // Nothing on this checkout recurs, so there is no agreement to record.
            return;
        }

        var attempts = await _attemptStore.GetBySessionAsync(session.SessionId);

        var settled = attempts
            .Where(attempt => attempt.State == PaymentAttemptState.Succeeded)
            .ToDictionary(attempt => attempt.ObligationId ?? string.Empty, StringComparer.Ordinal);

        var now = _clock.UtcNow;

        foreach (var group in groups)
        {
            var obligationId = CheckoutObligations.Recurring(group.Key);

            if (!settled.TryGetValue(obligationId, out var attempt))
            {
                // The customer never actually paid for this interval. Creating an active subscription would
                // hand out what they did not buy.
                continue;
            }

            // A checkout can complete more than once (a webhook and the reconciliation sweep both finishing
            // it), so the agreement is looked up before it is created.
            var existing = await _subscriptionManager.GetByObligationAsync(session.SessionId, obligationId);

            if (existing is not null)
            {
                continue;
            }

            var subscription = await BuildAsync(session, group.Key, group.Value, attempt, now);

            await _subscriptionManager.CreateAsync(subscription);

            // Creation is a transition too: it is the moment the subscriber first becomes entitled to what
            // they bought. Without telling the handlers here, a member-only role would only be granted the
            // next time something else happened to the agreement, which could be a month away.
            await _lifecycleHandlers.InvokeAsync(
                (handler, handlerContext) => handler.ChangedAsync(handlerContext),
                new SubscriptionLifecycleContext(subscription, SubscriptionStatus.Incomplete),
                _logger);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created subscription '{SubscriptionId}' from checkout session '{SessionId}'.", subscription.ItemId, session.SessionId);
            }
        }
    }

    private async Task<Subscription> BuildAsync(
        CheckoutSession session,
        CrestApps.OrchardCore.Checkout.BillingDurationKey interval,
        IList<CheckoutLineItem> lineItems,
        PaymentAttempt attempt,
        DateTime now)
    {
        var subscription = await _subscriptionManager.NewAsync();

        var isGuest = string.IsNullOrEmpty(session.OwnerId);

        subscription.Title = BuildTitle(lineItems);
        subscription.OwnerId = isGuest ? session.SessionId : session.OwnerId;
        subscription.OwnerKind = isGuest ? CustomerOwnerKind.Guest : CustomerOwnerKind.Authenticated;

        if (isGuest && session.TryGet<CheckoutContactInfo>(out var contact))
        {
            subscription.GuestContactName = contact.DisplayName;
            subscription.GuestContactEmail = contact.Email;
        }

        subscription.ReferenceType = session.ReferenceType;
        subscription.ReferenceId = session.ReferenceId;
        subscription.ReferenceVersionId = session.ReferenceVersionId;
        subscription.CheckoutSessionId = session.SessionId;
        subscription.ObligationId = attempt.ObligationId;
        subscription.ProviderKey = attempt.ProviderKey;

        // The provider reference is the agreement at the gateway, which is what a later webhook names. The
        // transaction id is only the first payment, so it is not what a subscription is correlated by.
        subscription.ProviderSubscriptionId = attempt.ProviderReference;
        subscription.GatewayMode = attempt.GatewayMode;
        subscription.Currency = attempt.Currency ?? session.Currency;
        subscription.Amount = attempt.ConfirmedAmount;
        subscription.TaxAmount = attempt.ConfirmedTaxAmount;
        subscription.BillingDuration = interval.Duration <= 0 ? 1 : interval.Duration;
        subscription.DurationType = interval.Type;

        // The lowest cap among the lines wins: billing past any line's limit charges for something the
        // customer did not agree to.
        subscription.BillingCycleLimit = lineItems
            .Select(lineItem => lineItem.Plan?.BillingCycleLimit)
            .Where(limit => limit > 0)
            .DefaultIfEmpty(null)
            .Min();

        subscription.Status = SubscriptionStatus.Active;
        subscription.CyclesBilled = 1;
        subscription.CurrentPeriodStartUtc = now;
        subscription.CurrentPeriodEndUtc = subscription.Advance(now);

        subscription.NextBillingUtc = subscription.BillingCycleLimit == 1
            ? null
            : subscription.CurrentPeriodEndUtc;

        subscription.CreatedUtc = now;
        subscription.UpdatedUtc = now;

        foreach (var lineItem in lineItems)
        {
            subscription.Lines.Add(new SubscriptionLine
            {
                ItemId = lineItem.ItemId,
                Description = lineItem.Description,
                Quantity = lineItem.Quantity,
                UnitPrice = lineItem.UnitPrice,
            });
        }

        await AddEntitlementsAsync(subscription, session);

        subscription.Events.Add(new SubscriptionEvent
        {
            CreatedUtc = now,
            Type = SubscriptionEventType.Created,
            Source = attempt.ProviderKey,
            Message = $"Created from checkout session '{session.SessionId}'.",
        });

        return subscription;
    }

    // Copies what the plan grants onto the subscription rather than reading the plan later. Editing a plan
    // afterwards must not silently change what an existing subscriber was sold.
    private async Task AddEntitlementsAsync(Subscription subscription, CheckoutSession session)
    {
        if (string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        ContentItem plan;

        try
        {
            plan = await _contentManager.GetAsync(session.ReferenceId);
        }
        catch (Exception exception)
        {
            // A checkout that is not for a content item, or a plan that has been removed, is not a reason to
            // fail the activation. The subscription is still real; it simply grants nothing on its own.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(exception, "Could not read the plan '{ReferenceId}' while creating a subscription.", session.ReferenceId);
            }

            return;
        }

        if (plan is null || !plan.TryGet<SubscriptionEntitlementPart>(out var part))
        {
            return;
        }

        foreach (var roleName in part.RoleNames ?? [])
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                continue;
            }

            subscription.Entitlements.Add(new SubscriptionEntitlement
            {
                Kind = SubscriptionConstants.EntitlementKinds.Role,
                Value = roleName.Trim(),
            });
        }
    }

    private static string BuildTitle(IEnumerable<CheckoutLineItem> lineItems)
    {
        var descriptions = lineItems
            .Select(lineItem => lineItem.Description)
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return descriptions.Length == 0 ? "Subscription" : string.Join(", ", descriptions);
    }
}
