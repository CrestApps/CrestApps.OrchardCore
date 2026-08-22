using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the feature profile selection for tenant onboarding subscriptions.
/// </summary>
public class FeatureProfilesViewModel
{
    /// <summary>
    /// Gets or sets the selected tenant feature profile identifier.
    /// </summary>
    public string FeatureProfile { get; set; }

    /// <summary>
    /// Gets or sets the available tenant feature profile options.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> FeatureProfiles { get; set; }
}
