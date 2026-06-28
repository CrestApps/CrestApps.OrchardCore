namespace CrestApps.OrchardCore.PhoneNumbers.Core.Models;

/// <summary>
/// Common contract implemented by provider-specific phone number verification settings that
/// support being enabled or disabled from the admin settings UI.
/// </summary>
public interface IPhoneNumberVerificationProviderSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider is enabled and available for selection.
    /// </summary>
    bool IsEnabled { get; set; }
}
