using CrestApps.OrchardCore.PhoneNumbers.Core.Models;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;

/// <summary>
/// Site settings for the Veriphone phone number verification provider.
/// </summary>
public sealed class VeriphonePhoneNumberVerificationSettings : IPhoneNumberVerificationProviderSettings
{
    /// <summary>
    /// The default Veriphone phone number verification endpoint.
    /// </summary>
    public const string DefaultEndpoint = "https://api.veriphone.io/v2/verify";

    /// <summary>
    /// Gets or sets a value indicating whether the provider is enabled and available for selection.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint used to verify phone numbers.
    /// </summary>
    public string Endpoint { get; set; } = DefaultEndpoint;

    /// <summary>
    /// Gets or sets the encrypted API key used to authenticate with Veriphone.
    /// </summary>
    public string ProtectedApiKey { get; set; }
}
