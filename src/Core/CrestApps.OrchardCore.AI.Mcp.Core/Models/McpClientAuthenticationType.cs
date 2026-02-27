namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public enum McpClientAuthenticationType
{
    Anonymous,
    ApiKey,
    Basic,
    OAuth2ClientCredentials,
    OAuth2PrivateKeyJwt,
    OAuth2Mtls,
    CustomHeaders,
}
