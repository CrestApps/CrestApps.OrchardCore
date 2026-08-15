using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public sealed class SubscriptionSummaryAdminViewModel
{
    public string SessionId { get; set; }

    public SubscriptionSessionStatus Status { get; set; }

    public string CustomerName { get; set; }

    public string CustomerEmail { get; set; }

    public string PlanTitle { get; set; }

    public string Currency { get; set; }

    public double DueNow { get; set; }

    public double? InitialAmount { get; set; }

    public double? RecurringAmount { get; set; }

    public int BillingDuration { get; set; }

    public DurationType? DurationType { get; set; }
}
