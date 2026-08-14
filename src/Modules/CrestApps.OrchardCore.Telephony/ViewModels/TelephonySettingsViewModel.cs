using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Telephony.ViewModels;

/// <summary>
/// View model for editing the default telephony provider.
/// </summary>
public class TelephonySettingsViewModel
{
    /// <summary>
    /// Gets or sets the technical name of the provider selected as the default.
    /// </summary>
    public string DefaultProvider { get; set; }

    /// <summary>
    /// Gets or sets the list of enabled providers available for selection.
    /// </summary>
    [BindNever]
    public SelectListItem[] Providers { get; set; }
}
