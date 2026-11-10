using System.Text.Json.Nodes;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Builds the store's OAuth 2.0 authorization server metadata (RFC 8414), which UCP account linking reads at
/// <c>/.well-known/oauth-authorization-server</c>, from the tenant's OpenID server settings.
/// </summary>
/// <remarks>
/// The values come from the options Orchard Core's OpenID server feature gives OpenIddict, so the document always says
/// what the server does: its issuer, its endpoints, its grant types and client authentication methods. UCP adds two
/// requirements the document states: PKCE with <c>S256</c>, and the <c>iss</c> parameter in authorization responses
/// (RFC 9207), which OpenIddict returns.
/// </remarks>
public static class OAuthAuthorizationServerMetadata
{
    /// <summary>
    /// The path of the document, after the tenant's base address.
    /// </summary>
    public const string Path = ".well-known/oauth-authorization-server";

    /// <summary>
    /// Returns the metadata, or <see langword="null"/> when the tenant's OpenID server isn't set up: it has neither an
    /// authorization endpoint nor a token endpoint.
    /// </summary>
    /// <param name="options">The OpenID server's options.</param>
    /// <param name="requestBaseUrl">The tenant's base address in this request, the issuer when the server sets
    /// none.</param>
    /// <param name="additionalScopes">Scopes the store offers on top of the server's own, such as UCP's.</param>
    public static JsonObject Build(OpenIddictServerOptions options, string requestBaseUrl, IEnumerable<string> additionalScopes)
    {
        if (options is null || (options.AuthorizationEndpointUris.Count == 0 && options.TokenEndpointUris.Count == 0))
        {
            return null;
        }

        // OpenIddict issues tokens with the configured issuer, or with the request's base address when there is none.
        var issuer = options.Issuer?.AbsoluteUri ?? (string.IsNullOrEmpty(requestBaseUrl) ? null : requestBaseUrl.TrimEnd('/') + "/");

        if (issuer is null || !Uri.TryCreate(issuer.EndsWith('/') ? issuer : issuer + "/", UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var json = new JsonObject
        {
            ["issuer"] = issuer,
        };

        var authorization = AddEndpoint(json, "authorization_endpoint", options.AuthorizationEndpointUris, baseUri);
        var token = AddEndpoint(json, "token_endpoint", options.TokenEndpointUris, baseUri);

        AddEndpoint(json, "jwks_uri", options.JsonWebKeySetEndpointUris, baseUri);

        var revocation = AddEndpoint(json, "revocation_endpoint", options.RevocationEndpointUris, baseUri);
        var introspection = AddEndpoint(json, "introspection_endpoint", options.IntrospectionEndpointUris, baseUri);

        AddEndpoint(json, "pushed_authorization_request_endpoint", options.PushedAuthorizationEndpointUris, baseUri);
        AddEndpoint(json, "userinfo_endpoint", options.UserInfoEndpointUris, baseUri);

        json["scopes_supported"] = Array(options.Scopes.Concat(additionalScopes ?? []));

        if (authorization)
        {
            json["response_types_supported"] = Array([OpenIddictConstants.ResponseTypes.Code]);

            var responseModes = options.ResponseModes
                .Where(mode => mode is OpenIddictConstants.ResponseModes.Query or OpenIddictConstants.ResponseModes.FormPost or OpenIddictConstants.ResponseModes.Fragment);

            if (responseModes.Any())
            {
                json["response_modes_supported"] = Array(responseModes);
            }
        }

        if (options.GrantTypes.Count > 0)
        {
            json["grant_types_supported"] = Array(options.GrantTypes);
        }

        if (authorization)
        {
            json["code_challenge_methods_supported"] = Array([OpenIddictConstants.CodeChallengeMethods.Sha256]);
        }

        var authenticationMethods = Array(options.ClientAuthenticationMethods);

        if (token)
        {
            json["token_endpoint_auth_methods_supported"] = authenticationMethods.DeepClone();
        }

        if (revocation)
        {
            json["revocation_endpoint_auth_methods_supported"] = authenticationMethods.DeepClone();
        }

        if (introspection)
        {
            json["introspection_endpoint_auth_methods_supported"] = authenticationMethods.DeepClone();
        }

        if (authorization)
        {
            json["authorization_response_iss_parameter_supported"] = true;
        }

        if (options.RequirePushedAuthorizationRequests)
        {
            json["require_pushed_authorization_requests"] = true;
        }

        return json;
    }

    private static bool AddEndpoint(JsonObject json, string name, List<Uri> uris, Uri baseUri)
    {
        if (uris is null || uris.Count == 0 || uris[0] is not { } uri)
        {
            return false;
        }

        // The server's endpoints are usually relative to the issuer, such as connect/token.
        json[name] = uri.IsAbsoluteUri
            ? uri.AbsoluteUri
            : new Uri(baseUri, uri.OriginalString.TrimStart('/')).AbsoluteUri;

        return true;
    }

    private static JsonArray Array(IEnumerable<string> values)
        => new([.. (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(value => (JsonNode)value)]);
}
