using CrestApps.Core.AI.Tooling.Instances;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the source specific fields captured for an HTTP API request tool instance.
/// </summary>
public class HttpApiRequestToolInstanceViewModel
{
    /// <summary>
    /// Gets or sets the base URL the request targets.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the HTTP method used to issue the request.
    /// </summary>
    public string HttpMethod { get; set; }

    /// <summary>
    /// Gets or sets the optional per-request timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the static headers always added to the request, formatted as a JSON object.
    /// </summary>
    public string DefaultHeaders { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI model may supply a relative path.
    /// </summary>
    public bool AllowModelProvidedPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI model may supply query string parameters.
    /// </summary>
    public bool AllowModelProvidedQuery { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI model may supply a request body.
    /// </summary>
    public bool AllowModelProvidedBody { get; set; }

    /// <summary>
    /// Gets or sets the authentication strategy applied to the request.
    /// </summary>
    public HttpApiRequestAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the header name used to send the API key.
    /// </summary>
    public string ApiKeyHeaderName { get; set; }

    /// <summary>
    /// Gets or sets the API key value.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the bearer token value.
    /// </summary>
    public string BearerToken { get; set; }

    /// <summary>
    /// Gets or sets the user name used for basic or OAuth 2.0 password grant authentication.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password used for basic or OAuth 2.0 password grant authentication.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the OAuth 2.0 token endpoint.
    /// </summary>
    public string TokenEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the OAuth 2.0 client identifier.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth 2.0 client secret.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the optional OAuth 2.0 scope.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key has already been stored.
    /// </summary>
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a bearer token has already been stored.
    /// </summary>
    public bool HasBearerToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a password has already been stored.
    /// </summary>
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a client secret has already been stored.
    /// </summary>
    public bool HasClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the selectable HTTP methods.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> HttpMethods { get; set; }

    /// <summary>
    /// Gets or sets the selectable authentication types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AuthenticationTypes { get; set; }
}
