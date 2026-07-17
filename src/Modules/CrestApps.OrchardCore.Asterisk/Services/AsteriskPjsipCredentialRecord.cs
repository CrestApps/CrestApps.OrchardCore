namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskPjsipCredentialRecord
{
    public string TenantName { get; set; }

    public string UserId { get; set; }

    public string SessionId { get; set; }

    public string InteractionId { get; set; }

    public string EndpointName { get; set; }

    public string AuthorizationUser { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string RevocationReason { get; set; }
}
