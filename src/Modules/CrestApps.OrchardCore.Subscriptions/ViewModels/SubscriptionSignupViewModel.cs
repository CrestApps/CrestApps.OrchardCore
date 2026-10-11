using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// What a visitor is offered when they choose to buy a plan.
/// </summary>
public class SubscriptionSignupViewModel
{
    /// <summary>
    /// Gets or sets the plan's content item identifier.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the prices the plan is currently offered at, in the order they should be shown.
    /// </summary>
    /// <remarks>
    /// Empty when the plan is sold at the single price on its product part, which is the common case and
    /// the one that renders as a plain "sign up" button rather than a form.
    /// </remarks>
    public IList<ProductPrice> Prices { get; set; } = [];

    /// <summary>
    /// Gets or sets the price selected when the visitor first sees the plan.
    /// </summary>
    public ProductPrice DefaultPrice { get; set; }

    /// <summary>
    /// Gets a value indicating whether the visitor has to be asked anything before checkout starts.
    /// </summary>
    /// <remarks>
    /// One fixed price needs no form: sending them straight into the checkout is fewer clicks and cannot
    /// be got wrong.
    /// </remarks>
    public bool RequiresChoice
        => Prices.Count > 1 ||
           (DefaultPrice is not null && (DefaultPrice.AllowCustomAmount || DefaultPrice.AllowQuantity));
}
