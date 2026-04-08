namespace CrestApps.Core.AI.A2A.Models;

public enum A2AClientAuthenticationType
{
    Anonymous,
    ApiKey,
    Basic,
    OAuth2ClientCredentials,
    OAuth2PrivateKeyJwt,
    OAuth2Mtls,
    CustomHeaders,
}
