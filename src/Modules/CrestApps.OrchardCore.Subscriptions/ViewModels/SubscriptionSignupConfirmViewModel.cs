using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// The page a plan's sign-up link shows: the plan, what it costs, and the button that starts its checkout.
/// </summary>
public class SubscriptionSignupConfirmViewModel
{
    /// <summary>
    /// Gets or sets the plan's content item identifier.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the plan's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the price the buyer chose, posted back with the button.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets how many the buyer is taking, posted back with the button.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the amount the buyer named, posted back with the button.
    /// </summary>
    public decimal? CustomAmount { get; set; }

    /// <summary>
    /// Gets or sets what each billing period costs.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency the amount is in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets how many units of <see cref="DurationType"/> each billing period lasts.
    /// </summary>
    public int BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the unit the billing period is counted in.
    /// </summary>
    public DurationType DurationType { get; set; }
}
