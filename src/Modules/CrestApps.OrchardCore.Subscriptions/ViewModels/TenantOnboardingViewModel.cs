using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class TenantOnboardingViewModel
{
    public string RecipeName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Recipes { get; set; }
}
