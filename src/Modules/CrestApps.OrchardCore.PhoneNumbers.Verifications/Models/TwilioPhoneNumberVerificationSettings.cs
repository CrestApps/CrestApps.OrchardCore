namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;

/// <summary>
/// Site settings for the Twilio Lookup phone number verification provider.
/// </summary>
public sealed class TwilioPhoneNumberVerificationSettings
{
    /// <summary>
    /// The default Twilio Lookup endpoint template.
    /// </summary>
    public const string DefaultEndpoint = "https://lookups.twilio.com/v2/PhoneNumbers/{PhoneNumber}";

    /// <summary>
    /// Gets or sets the Twilio Lookup endpoint template used to verify phone numbers.
    /// </summary>
    public string Endpoint { get; set; } = DefaultEndpoint;

    /// <summary>
    /// Gets or sets the Twilio Lookup authentication strategy.
    /// </summary>
    public TwilioPhoneNumberVerificationAuthenticationType AuthenticationType { get; set; }
        = TwilioPhoneNumberVerificationAuthenticationType.ApiKey;

    /// <summary>
    /// Gets or sets the Twilio API key SID used when <see cref="AuthenticationType"/> is
    /// <see cref="TwilioPhoneNumberVerificationAuthenticationType.ApiKey"/>.
    /// </summary>
    public string ApiKeySid { get; set; }

    /// <summary>
    /// Gets or sets the encrypted Twilio API key secret used when <see cref="AuthenticationType"/> is
    /// <see cref="TwilioPhoneNumberVerificationAuthenticationType.ApiKey"/>.
    /// </summary>
    public string ProtectedApiKeySecret { get; set; }

    /// <summary>
    /// Gets or sets the Twilio Account SID used when <see cref="AuthenticationType"/> is
    /// <see cref="TwilioPhoneNumberVerificationAuthenticationType.AccountCredentials"/>.
    /// </summary>
    public string AccountSid { get; set; }

    /// <summary>
    /// Gets or sets the encrypted Twilio Auth Token used when <see cref="AuthenticationType"/> is
    /// <see cref="TwilioPhoneNumberVerificationAuthenticationType.AccountCredentials"/>.
    /// </summary>
    public string ProtectedAuthToken { get; set; }

    /// <summary>
    /// Gets or sets the optional ISO 3166-1 alpha-2 country code used when a phone number is submitted in national format.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the optional comma-separated Twilio Lookup data packages requested with the <c>Fields</c> query parameter.
    /// </summary>
    public string Fields { get; set; }
}
