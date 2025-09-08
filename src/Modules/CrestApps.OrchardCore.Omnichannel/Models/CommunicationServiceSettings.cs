namespace CrestApps.OrchardCore.Omnichannel.Models;

public sealed class CommunicationServiceSettings
{
    public string SharedSecret { get; set; }           // HMAC secret configured in ACS

    public string WebhookValidationToken { get; set; } // Optional validation token
}
