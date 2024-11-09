using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class ServicePlanSubscriptionViewModel
{
    public string ContentItemId { get; set; }

    public string SessionId { get; set; }

    public string Step { get; set; }

    [BindNever]
    public IShape Content { get; set; }
}
