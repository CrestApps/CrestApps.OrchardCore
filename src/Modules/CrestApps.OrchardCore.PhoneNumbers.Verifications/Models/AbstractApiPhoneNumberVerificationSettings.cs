using CrestApps.OrchardCore.PhoneNumbers.Core.Models;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;

/// <summary>
/// Site settings for the AbstractAPI phone number verification provider.
/// </summary>
public sealed class AbstractApiPhoneNumberVerificationSettings : IPhoneNumberVerificationProviderSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider is enabled and available for selection.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint used to verify phone numbers.
    /// </summary>
    public string Endpoint { get; set; } = "https://phonevalidation.abstractapi.com/v1/";

    /// <summary>
    /// Gets or sets the encrypted API key used to authenticate with AbstractAPI.
    /// </summary>
    public string ProtectedApiKey { get; set; }

    /// <summary>
    /// Gets or sets the authentication strategy used by the provider.
    /// </summary>
    public PhoneNumberVerificationAuthenticationType AuthenticationType { get; set; }
        = PhoneNumberVerificationAuthenticationType.ApiKey;

    /// <summary>
    /// Gets or sets the username used when <see cref="AuthenticationType"/> is
    /// <see cref="PhoneNumberVerificationAuthenticationType.Basic"/>.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the encrypted password used when <see cref="AuthenticationType"/> is
    /// <see cref="PhoneNumberVerificationAuthenticationType.Basic"/>.
    /// </summary>
    public string ProtectedPassword { get; set; }
}
