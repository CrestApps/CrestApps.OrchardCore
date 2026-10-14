using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Decides what a subscription plan costs and how it recurs, from the pricing seam rather than the raw product
/// part. The checkout bills from it, and the sign-up page asks it first, so a plan that cannot be sold is refused
/// before a checkout is started rather than offered for nothing.
/// </summary>
public static class SubscriptionPlanPricing
{
    /// <summary>
    /// Resolves the price of a plan and the recurrence it is billed on.
    /// </summary>
    /// <param name="priceResolver">The pricing seam.</param>
    /// <param name="contentItem">The plan.</param>
    /// <param name="selection">The price, quantity or amount the buyer chose, if any.</param>
    public static async Task<SubscriptionPlanPrice> ResolveAsync(IPriceResolver priceResolver, ContentItem contentItem, CheckoutPriceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(priceResolver);
        ArgumentNullException.ThrowIfNull(contentItem);

        contentItem.TryGet<SubscriptionPart>(out var plan);

        var price = await priceResolver.ResolveAsync(new ProductSnapshotContext(contentItem)
        {
            PriceId = selection?.PriceId,
            Quantity = selection?.Quantity ?? 1,
            CustomAmount = selection?.CustomAmount,
        });

        if (price is null)
        {
            return new SubscriptionPlanPrice(plan, null, null, SubscriptionPlanPriceProblem.NoPrice);
        }

        // What recurs comes from the price the buyer chose when the product lists its prices, and from the plan
        // part when it does not. Those are the same thing expressed two ways, and a product that has been given
        // prices is the authority on its own terms.
        var recurrence = BuildRecurringPlan(price, plan);

        return recurrence is null
            ? new SubscriptionPlanPrice(plan, price, null, SubscriptionPlanPriceProblem.NotRecurring)
            : new SubscriptionPlanPrice(plan, price, recurrence, SubscriptionPlanPriceProblem.None);
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

    // The catalog keeps its own interval vocabulary so it stays independent of any purchasing pipeline; this is
    // the one place the two meet.
    private static DurationType MapInterval(BillingInterval? interval)
        => interval switch
        {
            BillingInterval.Day => DurationType.Day,
            BillingInterval.Week => DurationType.Week,
            BillingInterval.Year => DurationType.Year,
            _ => DurationType.Month,
        };
}

/// <summary>
/// Why a subscription plan cannot be sold.
/// </summary>
public enum SubscriptionPlanPriceProblem
{
    /// <summary>
    /// The plan can be sold.
    /// </summary>
    None,

    /// <summary>
    /// The pricing seam could not price the plan, for example because it has no currency and its content type
    /// sets no default currency.
    /// </summary>
    NoPrice,

    /// <summary>
    /// The plan's price is charged once, so it cannot be sold as something that renews.
    /// </summary>
    NotRecurring,
}

/// <summary>
/// The price of a subscription plan and how it recurs, or why it cannot be sold.
/// </summary>
/// <param name="Plan">The plan's subscription part, if it has one.</param>
/// <param name="Price">The resolved price, or <see langword="null"/> when the plan could not be priced.</param>
/// <param name="Recurrence">How the price recurs, or <see langword="null"/> when it does not.</param>
/// <param name="Problem">Why the plan cannot be sold, or <see cref="SubscriptionPlanPriceProblem.None"/>.</param>
public sealed record SubscriptionPlanPrice(SubscriptionPart Plan, PriceResult Price, RecurringPlan Recurrence, SubscriptionPlanPriceProblem Problem)
{
    /// <summary>
    /// Gets a value indicating whether the plan can be sold as a subscription.
    /// </summary>
    public bool CanBeBought => Problem == SubscriptionPlanPriceProblem.None;
}
