using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// View model for the Veriphone phone number verification settings.
/// </summary>
public class VeriphonePhoneNumberVerificationSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint used to verify phone numbers.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the API key used to authenticate with Veriphone.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key has already been stored.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }
}
