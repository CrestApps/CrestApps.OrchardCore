namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskPjsipRealtimeCredential
{
    public string TenantName { get; set; }

    public string SessionId { get; set; }

    public string EndpointName { get; set; }

    public string AuthorizationUser { get; set; }

    public string Password { get; set; }

    public string DisplayName { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public TimeSpan ContactExpiration { get; set; }

    public IReadOnlyList<string> Codecs { get; set; } = [];
}
