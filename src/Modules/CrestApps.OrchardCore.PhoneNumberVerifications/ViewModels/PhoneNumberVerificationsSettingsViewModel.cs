using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.ViewModels;

/// <summary>
/// View model for the core Phone Number Verifications settings.
/// </summary>
public class PhoneNumberVerificationsSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether just-in-time verification is enabled.
    /// </summary>
    public bool EnableJustInTimeVerification { get; set; }

    /// <summary>
    /// Gets or sets the number of days after which a verified phone number must be revalidated.
    /// </summary>
    public int RevalidationIntervalDays { get; set; }

    /// <summary>
    /// Gets or sets the key of the provider used by default.
    /// </summary>
    public string SelectedProvider { get; set; }

    /// <summary>
    /// Gets or sets the list of registered providers available for selection.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Providers { get; set; } = [];
}
