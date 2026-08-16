using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents a subscription session shape view model.
/// </summary>
public class SubscriptionViewModel : ShapeViewModel
{
    /// <summary>
    /// Gets or sets the subscription session rendered by the shape.
    /// </summary>
    public SubscriptionSession Subscription { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionViewModel"/> class.
    /// </summary>
    public SubscriptionViewModel()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionViewModel"/> class.
    /// </summary>
    /// <param name="subscription">The subscription session rendered by the shape.</param>
    public SubscriptionViewModel(SubscriptionSession subscription)
    {
        Subscription = subscription;
    }
}
