using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;

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
    public override Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            !SubscriptionCheckout.IsSubscriptionReference(session.ReferenceType))
        {
            return Task.CompletedTask;
        }

        // A subscription checkout that bills nothing recurring paid for nothing. Completing it would still run
        // every other completing handler: register the buyer's account, record a site to create, and so on. So
        // it is refused, and the engine rolls back whatever those handlers wrote.
        if (!global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutInvoice>(session, out var invoice) || invoice.GetRecurringGroups().Count == 0)
        {
            throw new InvalidOperationException($"Subscription checkout '{session.SessionId}' bills nothing that recurs, so it cannot be completed. The plan it was started for could not be priced as a subscription.");
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

        global::OrchardCore.Entities.EntityExtensions.TryGet<CheckoutPriceSelection>(session, out var selection);

        var pricing = await SubscriptionPlanPricing.ResolveAsync(_priceResolver, contentItem, selection);

        if (!pricing.CanBeBought)
        {
            // Nothing is billed, so nothing may be handed out either: completing refuses this checkout. Sign-up
            // checks the same thing first, so this only happens when the plan changed after the checkout began.
            _logger.LogWarning("Subscription plan '{ContentItemId}' cannot be bought ({Problem}), so checkout '{SessionId}' bills nothing and will not complete.", session.ReferenceId, pricing.Problem, session.SessionId);

            return;
        }

        var plan = pricing.Plan;
        var price = pricing.Price;
        var recurrence = pricing.Recurrence;

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
