using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Turns a published subscription service plan into the billing items of a checkout.
/// </summary>
/// <remarks>
/// This is the entry point for buying a subscription. The plan is the authority on what recurs and what is
/// charged once, so its billing items are attached to a single step: whatever else a checkout collects
/// (an account, a site, extra content), the plan is charged exactly once.
/// <para>
/// The price comes from the pricing seam rather than the raw product part, so a future pricing engine
/// changes what is charged without this handler changing at all.
/// </para>
/// </remarks>
public sealed class SubscriptionPlanCheckoutHandler : CheckoutHandlerBase
{
    /// <summary>
    /// The key of the step that carries the plan's billing items.
    /// </summary>
    public const string StepKey = "SubscriptionPlan";

    private readonly IContentManager _contentManager;
    private readonly IPriceResolver _priceResolver;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPlanCheckoutHandler"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager used to load the plan being bought.</param>
    /// <param name="priceResolver">The pricing seam that decides what the plan costs.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionPlanCheckoutHandler(
        IContentManager contentManager,
        IPriceResolver priceResolver,
        ILogger<SubscriptionPlanCheckoutHandler> logger,
        IStringLocalizer<SubscriptionPlanCheckoutHandler> stringLocalizer)
    {
        _contentManager = contentManager;
        _priceResolver = priceResolver;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(CheckoutFlowInitializingContext context)
    {
        // Concealment is decided per request rather than stored, so the plan step has to be hidden again
        // every time the session is loaded. It still carries the plan's charges: a concealed step is one
        // with nothing to ask, not one with nothing to bill.
        foreach (var step in context.Flow.Session.Steps)
        {
            if (string.Equals(step.Key, StepKey, StringComparison.Ordinal))
            {
                step.Conceal = true;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (!SubscriptionCheckout.IsSubscriptionReference(session.ReferenceType) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var contentItem = await _contentManager.GetAsync(session.ReferenceId);

        if (contentItem is null)
        {
            _logger.LogWarning("Checkout session '{SessionId}' references subscription plan '{ContentItemId}', which no longer resolves to a content item.", session.SessionId, session.ReferenceId);

            return;
        }

        contentItem.TryGet<SubscriptionPart>(out var plan);

        global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutPriceSelection>(session, out var selection);

        var price = await _priceResolver.ResolveAsync(new ProductSnapshotContext(contentItem)
        {
            PriceId = selection?.PriceId,
            Quantity = selection?.Quantity ?? 1,
            CustomAmount = selection?.CustomAmount,
        });

        if (price is null)
        {
            _logger.LogWarning("Subscription plan '{ContentItemId}' has no resolvable price, so it cannot be bought.", session.ReferenceId);

            return;
        }

        // What recurs comes from the price the buyer chose when the product lists its prices, and from the
        // plan part when it does not. Those are the same thing expressed two ways, and a product that has
        // been given prices is the authority on its own terms.
        var recurrence = BuildRecurringPlan(price, plan);

        if (recurrence is null)
        {
            _logger.LogWarning("Subscription plan '{ContentItemId}' resolves to a price that does not recur, so it cannot be sold as a subscription.", session.ReferenceId);

            return;
        }

        // The version is recorded so a plan edited after the customer started still bills what they were
        // shown, and so the agreement can name the exact version that was agreed to.
        session.ReferenceVersionId = contentItem.ContentItemVersionId;

        if (string.IsNullOrEmpty(session.Currency))
        {
            session.Currency = price.Currency;
        }

        var billingItems = new List<BillingItem>
        {
            new()
            {
                ItemId = contentItem.ContentItemVersionId,
                Description = BuildDescription(contentItem, price),

                // The subtotal, not the unit price: a price the buyer may take several of bills for all of
                // them, every cycle.
                Amount = price.Subtotal,
                Plan = recurrence,

                // Only a fixed price names a reusable offer. An amount the buyer chose has none, so the
                // gateway is given the amount inline instead of a price to look up.
                PriceId = price.Price is { AllowCustomAmount: false } ? price.Price.PriceId : null,
            },
        };

        var setupFee = price.Price?.SetupFee ?? plan?.InitialAmount;
        var setupFeeDescription = price.Price is not null ? price.Price.SetupFeeDescription : plan?.InitialAmountDescription;

        if (setupFee > 0m)
        {
            billingItems.Add(new BillingItem
            {
                ItemId = contentItem.ContentItemVersionId + SubscriptionConstants.InitialFeeIdPrefix,
                Description = string.IsNullOrEmpty(setupFeeDescription)
                    ? S["Setup fee"].Value
                    : setupFeeDescription,
                Amount = setupFee.Value,
            });
        }

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = StepKey,
            Title = S["Plan"],
            Description = contentItem.DisplayText,

            // The plan is what the customer already chose, so it does not ask them for anything. It exists
            // as a step only because billing items belong to steps.
            Order = 0,
            CollectData = false,
            Conceal = true,
            BillingItems = [.. billingItems],
        });
    }

    private static RecurringPlan BuildRecurringPlan(PriceResult price, SubscriptionPart plan)
    {
        if (price.Price is not null)
        {
            if (price.Price.Kind != PriceKind.Recurring)
            {
                return null;
            }

            return new RecurringPlan
            {
                BillingDuration = price.Price.BillingDuration ?? 1,
                DurationType = MapInterval(price.Price.Interval),
                BillingCycleLimit = price.Price.BillingCycleLimit,
                StartDayDelay = price.Price.StartDayDelay,
                TrialDays = price.Price.TrialDays,
            };
        }

        if (plan is null)
        {
            return null;
        }

        return new RecurringPlan
        {
            BillingDuration = plan.BillingDuration,
            DurationType = plan.DurationType,
            BillingCycleLimit = plan.BillingCycleLimit,
            StartDayDelay = plan.SubscriptionDayDelay,
            TrialDays = plan.TrialDays,
        };
    }

    // The catalog keeps its own interval vocabulary so it stays independent of any purchasing pipeline;
    // this is the one place the two meet.
    private static DurationType MapInterval(BillingInterval? interval)
        => interval switch
        {
            BillingInterval.Day => DurationType.Day,
            BillingInterval.Week => DurationType.Week,
            BillingInterval.Year => DurationType.Year,
            _ => DurationType.Month,
        };

    // The plan's own title unless the buyer chose between named prices, in which case the name they picked
    // is what they expect to see on the invoice.
    private static string BuildDescription(ContentItem contentItem, PriceResult price)
    {
        var name = price.Price?.Name;
        var description = string.IsNullOrWhiteSpace(name)
            ? contentItem.DisplayText
            : contentItem.DisplayText + " - " + name;

        return price.Quantity > 1 ? description + " x " + price.Quantity : description;
    }
}
