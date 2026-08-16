namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the subscription summaries displayed for a subscriber.
/// </summary>
public class ListSubscriptionSummariesViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListSubscriptionSummariesViewModel"/> class.
    /// </summary>
    public ListSubscriptionSummariesViewModel()
    {
    }

    /// <summary>
    /// Gets or sets the subscription summaries to display.
    /// </summary>
    public IList<SubscriptionsSummaryViewModel> Subscriptions { get; set; }
}
