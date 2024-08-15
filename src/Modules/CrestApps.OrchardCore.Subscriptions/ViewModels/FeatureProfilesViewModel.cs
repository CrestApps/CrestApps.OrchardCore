using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class FeatureProfilesViewModel
{
    public string FeatureProfile { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> FeatureProfiles { get; set; }
}
