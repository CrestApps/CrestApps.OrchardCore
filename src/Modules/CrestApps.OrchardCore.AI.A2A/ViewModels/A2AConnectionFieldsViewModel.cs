using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.A2A.ViewModels;

/// <summary>
/// Represents the view model for a2 a connection fields.
/// </summary>
public class A2AConnectionFieldsViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the endpoint.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public ClientAuthenticationType AuthenticationType { get; set; }

    // API Key.

    /// <summary>
    /// Gets or sets the api key header name.
    /// </summary>
    public string ApiKeyHeaderName { get; set; }

    /// <summary>
    /// Gets or sets the api key prefix.
    /// </summary>
    public string ApiKeyPrefix { get; set; }

    /// <summary>
    /// Gets or sets the api key.
    /// </summary>
    public string ApiKey { get; set; }

    // Basic.

    /// <summary>
    /// Gets or sets the basic username.
    /// </summary>
    public string BasicUsername { get; set; }

    /// <summary>
    /// Gets or sets the basic password.
    /// </summary>
    public string BasicPassword { get; set; }

    // OAuth 2.0.

    /// <summary>
    /// Gets or sets the o auth2 token endpoint.
    /// </summary>
    public string OAuth2TokenEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the o auth2 client id.
    /// </summary>
    public string OAuth2ClientId { get; set; }

    /// <summary>
    /// Gets or sets the o auth2 client secret.
    /// </summary>
    public string OAuth2ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the o auth2 scopes.
    /// </summary>
    public string OAuth2Scopes { get; set; }

    // OAuth 2.0 Private Key JWT.

    /// <summary>
    /// Gets or sets the o auth2 private key.
    /// </summary>
    public string OAuth2PrivateKey { get; set; }

    /// <summary>
    /// Gets or sets the o auth2 key id.
    /// </summary>
    public string OAuth2KeyId { get; set; }

    // OAuth 2.0 mTLS.

    /// <summary>
    /// Gets or sets the o auth2 client certificate.
    /// </summary>
    public string OAuth2ClientCertificate { get; set; }

    /// <summary>
    /// Gets or sets the o auth2 client certificate password.
    /// </summary>
    public string OAuth2ClientCertificatePassword { get; set; }

    // Custom headers.

    /// <summary>
    /// Gets or sets the additional headers.
    /// </summary>
    public string AdditionalHeaders { get; set; }

    // Read-only flags for secured placeholder display.

    /// <summary>
    /// Gets or sets a value indicating whether has api key.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has basic password.
    /// </summary>
    [BindNever]
    public bool HasBasicPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has o auth2 client secret.
    /// </summary>
    [BindNever]
    public bool HasOAuth2ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has o auth2 private key.
    /// </summary>
    [BindNever]
    public bool HasOAuth2PrivateKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has o auth2 client certificate.
    /// </summary>
    [BindNever]
    public bool HasOAuth2ClientCertificate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has o auth2 client certificate password.
    /// </summary>
    [BindNever]
    public bool HasOAuth2ClientCertificatePassword { get; set; }

    /// <summary>
    /// Gets or sets the schema.
    /// </summary>
    [BindNever]
    public string Schema { get; set; }
}
