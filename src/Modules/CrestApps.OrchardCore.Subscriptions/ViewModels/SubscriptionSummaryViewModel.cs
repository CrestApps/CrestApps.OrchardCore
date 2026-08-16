namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents aggregate subscription metrics displayed in a summary widget.
/// </summary>
public class SubscriptionSummaryViewModel
{
    /// <summary>
    /// Gets or sets the total number of subscription sessions.
    /// </summary>
    public int TotalSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of active subscriptions.
    /// </summary>
    public int ActiveSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of pending subscription sessions.
    /// </summary>
    public int PendingSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of completed subscription sessions.
    /// </summary>
    public int CompletedSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the total revenue amount from successful subscription payments.
    /// </summary>
    public double TotalRevenue { get; set; }
}
