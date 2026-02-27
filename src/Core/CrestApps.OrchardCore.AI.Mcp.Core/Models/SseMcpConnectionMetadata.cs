namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public sealed class SseMcpConnectionMetadata
{
    public Uri Endpoint { get; set; }

    public McpClientAuthenticationType AuthenticationType { get; set; }

    // API Key authentication.
    public string ApiKeyHeaderName { get; set; }

    public string ApiKeyPrefix { get; set; }

    public string ApiKey { get; set; }

    // Basic authentication.
    public string BasicUsername { get; set; }

    public string BasicPassword { get; set; }

    // OAuth 2.0 Client Credentials.
    public string OAuth2TokenEndpoint { get; set; }

    public string OAuth2ClientId { get; set; }

    public string OAuth2ClientSecret { get; set; }

    public string OAuth2Scopes { get; set; }

    // OAuth 2.0 Private Key JWT.
    public string OAuth2PrivateKey { get; set; }

    public string OAuth2KeyId { get; set; }

    // OAuth 2.0 Mutual TLS (mTLS).
    public string OAuth2ClientCertificate { get; set; }

    public string OAuth2ClientCertificatePassword { get; set; }

    // Custom headers (advanced / legacy).
    public Dictionary<string, string> AdditionalHeaders { get; set; }
}
