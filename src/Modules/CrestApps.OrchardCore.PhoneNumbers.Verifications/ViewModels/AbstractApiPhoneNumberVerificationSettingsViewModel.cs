using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.ViewModels;

/// <summary>
/// View model for the AbstractAPI phone number verification settings.
/// </summary>
public class AbstractApiPhoneNumberVerificationSettingsViewModel
{
    /// <summary>
    /// Gets or sets the API endpoint used to verify phone numbers.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the API key used to authenticate with AbstractAPI.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the authentication strategy used by the provider.
    /// </summary>
    public PhoneNumberVerificationAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the username used for basic authentication.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password used for basic authentication.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the client identifier used for client credentials authentication.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret used for client credentials authentication.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key has already been stored.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a password has already been stored.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a client secret has already been stored.
    /// </summary>
    [BindNever]
    public bool HasClientSecret { get; set; }
}
