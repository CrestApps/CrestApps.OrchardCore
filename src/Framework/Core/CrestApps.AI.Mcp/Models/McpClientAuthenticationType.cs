namespace CrestApps.AI.Mcp.Models;

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
