namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;

/// <summary>
/// Describes the Twilio Lookup authentication strategy.
/// </summary>
public enum TwilioPhoneNumberVerificationAuthenticationType
{
    /// <summary>
    /// HTTP Basic authentication using a Twilio API key SID and API key secret.
    /// </summary>
    ApiKey = 0,

    /// <summary>
    /// HTTP Basic authentication using a Twilio Account SID and Auth Token.
    /// </summary>
    AccountCredentials = 1,
}
