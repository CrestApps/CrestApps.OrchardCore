namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents a single subscription summary displayed to a subscriber.
/// </summary>
public class SubscriptionsSummaryViewModel
{
    /// <summary>
    /// Gets or sets the date and time when the subscription started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the subscription expires, when applicable.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the title of the subscribed service plan.
    /// </summary>
    public string ServicePlanTitle { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the subscription session.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the subscription is active.
    /// </summary>
    public bool IsActive { get; set; }
}
