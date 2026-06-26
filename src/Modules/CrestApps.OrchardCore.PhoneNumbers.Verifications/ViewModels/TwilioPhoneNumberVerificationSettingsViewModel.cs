using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// View model for the Twilio Lookup phone number verification settings.
/// </summary>
public class TwilioPhoneNumberVerificationSettingsViewModel
{
    /// <summary>
    /// Gets or sets the Twilio Lookup endpoint template used to verify phone numbers.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the Twilio Lookup authentication strategy.
    /// </summary>
    public TwilioPhoneNumberVerificationAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the Twilio API key SID used for API key authentication.
    /// </summary>
    public string ApiKeySid { get; set; }

    /// <summary>
    /// Gets or sets the Twilio API key secret used for API key authentication.
    /// </summary>
    public string ApiKeySecret { get; set; }

    /// <summary>
    /// Gets or sets the Twilio Account SID used for account credential authentication.
    /// </summary>
    public string AccountSid { get; set; }

    /// <summary>
    /// Gets or sets the Twilio Auth Token used for account credential authentication.
    /// </summary>
    public string AuthToken { get; set; }

    /// <summary>
    /// Gets or sets the optional ISO 3166-1 alpha-2 country code used when a phone number is submitted in national format.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the optional comma-separated Twilio Lookup data packages requested with the <c>Fields</c> query parameter.
    /// </summary>
    public string Fields { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key secret has already been stored.
    /// </summary>
    [BindNever]
    public bool HasApiKeySecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an Auth Token has already been stored.
    /// </summary>
    [BindNever]
    public bool HasAuthToken { get; set; }
}
