using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionSettingsViewModel
{
    public string Currency { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Currencies { get; set; }
}
