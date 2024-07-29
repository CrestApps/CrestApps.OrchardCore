using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.CrestApps.Subscriptions.Core.Models;

namespace OrchardCore.CrestApps.Subscriptions.ViewModels;

public class SubscriptionPartViewModel
{
    public double? InitialAmount { get; set; }

    public double? BillingAmount { get; set; }

    public int BillingDuration { get; set; }

    public BillingDurationType DurationType { get; set; }

    public int? BillingCycleLimit { get; set; }

    public int? SubscriptionDayDelay { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> DurationTypes { get; set; }
}
