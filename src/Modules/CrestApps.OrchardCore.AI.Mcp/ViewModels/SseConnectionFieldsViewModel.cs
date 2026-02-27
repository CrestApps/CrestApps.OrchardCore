using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class SseConnectionFieldsViewModel
{
    public string Endpoint { get; set; }

    public McpClientAuthenticationType AuthenticationType { get; set; }

    // API Key fields.
    public string ApiKeyHeaderName { get; set; }

    public string ApiKeyPrefix { get; set; }

    public string ApiKey { get; set; }

    // Basic auth fields.
    public string BasicUsername { get; set; }

    public string BasicPassword { get; set; }

    // OAuth 2.0 shared fields.
    public string OAuth2TokenEndpoint { get; set; }

    public string OAuth2ClientId { get; set; }

    public string OAuth2Scopes { get; set; }

    // OAuth 2.0 Client Credentials fields.
    public string OAuth2ClientSecret { get; set; }

    // OAuth 2.0 Private Key JWT fields.
    public string OAuth2PrivateKey { get; set; }

    public string OAuth2KeyId { get; set; }

    // OAuth 2.0 mTLS fields.
    public string OAuth2ClientCertificate { get; set; }

    public string OAuth2ClientCertificatePassword { get; set; }

    // Custom headers (advanced).
    public string AdditionalHeaders { get; set; }

    [BindNever]
    public string Schema { get; set; }

    [BindNever]
    public bool HasApiKey { get; set; }

    [BindNever]
    public bool HasBasicPassword { get; set; }

    [BindNever]
    public bool HasOAuth2ClientSecret { get; set; }

    [BindNever]
    public bool HasOAuth2PrivateKey { get; set; }

    [BindNever]
    public bool HasOAuth2ClientCertificate { get; set; }

    [BindNever]
    public bool HasOAuth2ClientCertificatePassword { get; set; }
}
