using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// View model for the core Phone Number Verifications settings.
/// </summary>
public class PhoneNumberVerificationsSettingsViewModel
{
    /// <summary>
    /// Gets or sets the number of days after which a verified phone number must be revalidated.
    /// </summary>
    public int RevalidationIntervalDays { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of consecutive failed verification attempts before a record
    /// stops being retried automatically.
    /// </summary>
    public int MaxVerificationAttempts { get; set; }

    /// <summary>
    /// Gets or sets the delay, in milliseconds, applied between consecutive provider verification
    /// requests during background processing, used to avoid provider rate limits.
    /// </summary>
    public int RequestDelayMilliseconds { get; set; }

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
