using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
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
    private readonly IProductSnapshotResolver _snapshotResolver;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPlanCheckoutHandler"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager used to load the plan being bought.</param>
    /// <param name="snapshotResolver">The resolver that projects the plan into a sellable snapshot.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionPlanCheckoutHandler(
        IContentManager contentManager,
        IProductSnapshotResolver snapshotResolver,
        ILogger<SubscriptionPlanCheckoutHandler> logger,
        IStringLocalizer<SubscriptionPlanCheckoutHandler> stringLocalizer)
    {
        _contentManager = contentManager;
        _snapshotResolver = snapshotResolver;
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

        if (contentItem is null || !contentItem.TryGet<SubscriptionPart>(out var plan))
        {
            _logger.LogWarning("Checkout session '{SessionId}' references subscription plan '{ContentItemId}', which no longer resolves to a subscription.", session.SessionId, session.ReferenceId);

            return;
        }

        var snapshot = await _snapshotResolver.ResolveAsync(new ProductSnapshotContext(contentItem));

        if (snapshot is null)
        {
            _logger.LogWarning("Subscription plan '{ContentItemId}' has no resolvable price, so it cannot be bought.", session.ReferenceId);

            return;
        }

        // The version is recorded so a plan edited after the customer started still bills what they were
        // shown, and so the agreement can name the exact version that was agreed to.
        session.ReferenceVersionId = contentItem.ContentItemVersionId;

        if (string.IsNullOrEmpty(session.Currency))
        {
            session.Currency = snapshot.Currency;
        }

        var billingItems = new List<BillingItem>
        {
            new()
            {
                ItemId = contentItem.ContentItemVersionId,
                Description = contentItem.DisplayText,
                Amount = snapshot.UnitPrice,
                Plan = new RecurringPlan
                {
                    BillingDuration = plan.BillingDuration,
                    DurationType = plan.DurationType,
                    BillingCycleLimit = plan.BillingCycleLimit,
                    StartDayDelay = plan.SubscriptionDayDelay,
                    TrialDays = plan.TrialDays,
                },
            },
        };

        if (plan.InitialAmount > 0)
        {
            billingItems.Add(new BillingItem
            {
                ItemId = contentItem.ContentItemVersionId + SubscriptionConstants.InitialFeeIdPrefix,
                Description = string.IsNullOrEmpty(plan.InitialAmountDescription)
                    ? S["Setup fee"].Value
                    : plan.InitialAmountDescription,
                Amount = plan.InitialAmount.Value,
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
}
