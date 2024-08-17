using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class PaymentMethodsViewModel
{
    public string PaymentMethod { get; set; }

    [BindNever]
    public SelectListItem[] PaymentMethods { get; set; }

    [BindNever]
    public SubscriptionFlow Flow { get; internal set; }
}
