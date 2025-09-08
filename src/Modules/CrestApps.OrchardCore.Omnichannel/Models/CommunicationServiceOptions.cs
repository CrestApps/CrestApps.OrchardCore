namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// Options to configure ACS endpoint security
/// </summary>
public sealed class CommunicationServiceOptions
{
    public string SharedSecret { get; set; }           // HMAC secret configured in ACS

    public string WebhookValidationToken { get; set; } // Optional validation token
}
