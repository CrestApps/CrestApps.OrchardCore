using CrestApps.Core.AI.A2A.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.A2A.ViewModels;

public class A2AConnectionFieldsViewModel
{
    public string DisplayText { get; set; }

    public string Endpoint { get; set; }

    public A2AClientAuthenticationType AuthenticationType { get; set; }

    // API Key.
    public string ApiKeyHeaderName { get; set; }

    public string ApiKeyPrefix { get; set; }

    public string ApiKey { get; set; }

    // Basic.
    public string BasicUsername { get; set; }

    public string BasicPassword { get; set; }

    // OAuth 2.0.
    public string OAuth2TokenEndpoint { get; set; }

    public string OAuth2ClientId { get; set; }

    public string OAuth2ClientSecret { get; set; }

    public string OAuth2Scopes { get; set; }

    // OAuth 2.0 Private Key JWT.
    public string OAuth2PrivateKey { get; set; }

    public string OAuth2KeyId { get; set; }

    // OAuth 2.0 mTLS.
    public string OAuth2ClientCertificate { get; set; }

    public string OAuth2ClientCertificatePassword { get; set; }

    // Custom headers.
    public string AdditionalHeaders { get; set; }

    // Read-only flags for secured placeholder display.
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

    [BindNever]
    public string Schema { get; set; }
}
