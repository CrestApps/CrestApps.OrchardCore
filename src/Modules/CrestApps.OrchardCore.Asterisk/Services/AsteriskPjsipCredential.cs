namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskPjsipCredential
{
    public string TenantName { get; set; }

    public string SessionId { get; set; }

    public string EndpointName { get; set; }

    public string AuthorizationUser { get; set; }

    public string Password { get; set; }

    public string SipUri { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}
