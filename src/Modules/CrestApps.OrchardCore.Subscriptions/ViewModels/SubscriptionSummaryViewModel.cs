namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionSummaryViewModel
{
    public int TotalSubscriptions { get; set; }

    public int ActiveSubscriptions { get; set; }

    public int PendingSubscriptions { get; set; }

    public int CompletedSubscriptions { get; set; }

    public double TotalRevenue { get; set; }
}
