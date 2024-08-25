using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class DisplaySubscriptionViewModel
{
    public double Price { get; set; }

    public string InitialAmountDescription { get; set; }

    public double? InitialAmount { get; set; }

    public int BillingDuration { get; set; }

    public DurationType DurationType { get; set; }

    public int? BillingCycleLimit { get; set; }

    public int? SubscriptionDayDelay { get; set; }
}
