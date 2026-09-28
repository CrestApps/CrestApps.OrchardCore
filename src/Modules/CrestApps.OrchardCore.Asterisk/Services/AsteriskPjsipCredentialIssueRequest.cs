namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskPjsipCredentialIssueRequest
{
    public string UserId { get; set; }

    public string DisplayName { get; set; }

    public string SessionId { get; set; }

    public string InteractionId { get; set; }

    public string SipDomain { get; set; }

    public TimeSpan CredentialLifetime { get; set; }

    public TimeSpan ContactExpiration { get; set; }

    public IReadOnlyList<string> Codecs { get; set; } = [];
}
