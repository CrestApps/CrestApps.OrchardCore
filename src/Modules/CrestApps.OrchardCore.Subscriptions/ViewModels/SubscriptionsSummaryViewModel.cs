namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionsSummaryViewModel
{
    public DateTime StartedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string ServicePlanTitle { get; set; }

    public string SessionId { get; set; }
    public bool IsActive { get; internal set; }
}
