using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents tenant onboarding part settings for selecting a setup recipe.
/// </summary>
public class TenantOnboardingViewModel
{
    /// <summary>
    /// Gets or sets the selected setup recipe name.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the setup recipes available for tenant onboarding.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Recipes { get; set; }
}
