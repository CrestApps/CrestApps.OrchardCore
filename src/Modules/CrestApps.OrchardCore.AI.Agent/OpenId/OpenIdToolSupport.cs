using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// What the OpenID tools share: finding an application the way the Applications admin page does, reading its fields
/// the way the Edit page shows them, and the addresses of the admin pages. Nothing here reads a client secret.
/// </summary>
internal static class OpenIdToolSupport
{
    /// <summary>
    /// The name of the authorization code flow in the arguments and results of the tools.
    /// </summary>
    public const string AuthorizationCodeFlow = "authorizationCode";

    /// <summary>
    /// The name of the client credentials flow in the arguments and results of the tools.
    /// </summary>
    public const string ClientCredentialsFlow = "clientCredentials";

    /// <summary>
    /// The name of the hybrid flow in the arguments and results of the tools.
    /// </summary>
    public const string HybridFlow = "hybrid";

    /// <summary>
    /// The name of the implicit flow in the arguments and results of the tools.
    /// </summary>
    public const string ImplicitFlow = "implicit";

    /// <summary>
    /// The name of the password flow in the arguments and results of the tools.
    /// </summary>
    public const string PasswordFlow = "password";

    /// <summary>
    /// The name of the refresh token flow in the arguments and results of the tools.
    /// </summary>
    public const string RefreshTokenFlow = "refreshToken";

    /// <summary>
    /// The name of the logout endpoint in the arguments and results of the tools.
    /// </summary>
    public const string LogoutEndpoint = "logout";

    /// <summary>
    /// The name of the introspection endpoint in the arguments and results of the tools.
    /// </summary>
    public const string IntrospectionEndpoint = "introspection";

    /// <summary>
    /// The name of the revocation endpoint in the arguments and results of the tools.
    /// </summary>
    public const string RevocationEndpoint = "revocation";

    /// <summary>
    /// The flows the Applications admin page offers.
    /// </summary>
    public static readonly string[] Flows =
    [
        AuthorizationCodeFlow,
        ClientCredentialsFlow,
        HybridFlow,
        ImplicitFlow,
        PasswordFlow,
        RefreshTokenFlow,
    ];

    /// <summary>
    /// The endpoints the Applications admin page offers.
    /// </summary>
    public static readonly string[] Endpoints =
    [
        LogoutEndpoint,
        IntrospectionEndpoint,
        RevocationEndpoint,
    ];

    /// <summary>
    /// The consent types the Applications admin page offers.
    /// </summary>
    public static readonly string[] ConsentTypes =
    [
        OpenIddictConstants.ConsentTypes.Explicit,
        OpenIddictConstants.ConsentTypes.Implicit,
        OpenIddictConstants.ConsentTypes.External,
    ];

    // The permission older versions stored for the logout endpoint. The Edit page still reads it.
    private const string LegacyLogoutPermission = "ept:logout";

    /// <summary>
    /// Finds the application a tool is given by the ID of the admin page, by its client ID, or by both.
    /// </summary>
    /// <param name="manager">The application manager.</param>
    /// <param name="id">The ID the admin page uses, or <see langword="null"/>.</param>
    /// <param name="clientId">The client ID, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The application, or the sentence the tool returns when there is none or the two IDs disagree.</returns>
    public static async Task<(object Application, string Error)> FindAsync(
        IOpenIdApplicationManager manager,
        string id,
        string clientId,
        CancellationToken cancellationToken)
    {
        id = id?.Trim();
        clientId = clientId?.Trim();

        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(clientId))
        {
            return (null, "give id or clientId of the application. Use listOpenIdApplications to find them.");
        }

        if (string.IsNullOrEmpty(id))
        {
            var byClientId = await manager.FindByClientIdAsync(clientId, cancellationToken);

            return byClientId is null
                ? (null, $"there is no OpenID application with the client id '{clientId}'. Use listOpenIdApplications to find it.")
                : (byClientId, null);
        }

        var application = await manager.FindByPhysicalIdAsync(id, cancellationToken);

        if (application is null)
        {
            return (null, $"there is no OpenID application with the id '{id}'. Use listOpenIdApplications to find it.");
        }

        if (!string.IsNullOrEmpty(clientId) &&
            !string.Equals(await manager.GetClientIdAsync(application, cancellationToken), clientId, StringComparison.Ordinal))
        {
            return (null, $"the application with the id '{id}' does not have the client id '{clientId}'. Use listOpenIdApplications to get both.");
        }

        return (application, null);
    }

    /// <summary>
    /// Reads the fields of an application the way the Edit page of the Applications admin area shows them: a flow or
    /// an endpoint is allowed when the application holds the permissions that page checks for it.
    /// </summary>
    /// <param name="manager">The application manager.</param>
    /// <param name="application">The application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The fields of the application, without any client secret.</returns>
    public static async Task<OpenIdApplicationState> ReadAsync(
        IOpenIdApplicationManager manager,
        object application,
        CancellationToken cancellationToken)
    {
        var permissions = (await manager.GetPermissionsAsync(application, cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var requirements = (await manager.GetRequirementsAsync(application, cancellationToken)).ToHashSet(StringComparer.Ordinal);

        var state = new OpenIdApplicationState
        {
            Id = await manager.GetPhysicalIdAsync(application, cancellationToken),
            ClientId = await manager.GetClientIdAsync(application, cancellationToken),
            DisplayName = await manager.GetDisplayNameAsync(application, cancellationToken),
            Type = await manager.GetClientTypeAsync(application, cancellationToken),
            ConsentType = await manager.GetConsentTypeAsync(application, cancellationToken),
            RedirectUris = [.. await manager.GetRedirectUrisAsync(application, cancellationToken)],
            PostLogoutRedirectUris = [.. await manager.GetPostLogoutRedirectUrisAsync(application, cancellationToken)],
            RequirePkce = requirements.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange),
            RequirePushedAuthorizationRequests = requirements.Contains(OpenIddictConstants.Requirements.Features.PushedAuthorizationRequests),
            Roles = [.. await manager.GetRolesAsync(application, cancellationToken)],
            Scopes = permissions
                .Where(permission => permission.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.Ordinal))
                .Select(permission => permission.Substring(OpenIddictConstants.Permissions.Prefixes.Scope.Length))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

        var authorizationCodeGrant = permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        var implicitGrant = permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.Implicit);

        if (authorizationCodeGrant && permissions.Contains(OpenIddictConstants.Permissions.ResponseTypes.Code))
        {
            state.Flows.Add(AuthorizationCodeFlow);
        }

        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials))
        {
            state.Flows.Add(ClientCredentialsFlow);
        }

        // The hybrid flow has no grant type of its own: it is the authorization code grant with the implicit grant.
        if (authorizationCodeGrant &&
            implicitGrant &&
            (permissions.Contains(OpenIddictConstants.Permissions.ResponseTypes.CodeIdToken) ||
                permissions.Contains(OpenIddictConstants.Permissions.ResponseTypes.CodeIdTokenToken) ||
                permissions.Contains(OpenIddictConstants.Permissions.ResponseTypes.CodeToken)))
        {
            state.Flows.Add(HybridFlow);
        }

        if (implicitGrant &&
            (permissions.Contains(OpenIddictConstants.Permissions.ResponseTypes.IdToken) ||
                permissions.Contains(OpenIddictConstants.Permissions.ResponseTypes.IdTokenToken) ||
                permissions.Contains(OpenIddictConstants.Permissions.ResponseTypes.Token)))
        {
            state.Flows.Add(ImplicitFlow);
        }

        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.Password))
        {
            state.Flows.Add(PasswordFlow);
        }

        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.RefreshToken))
        {
            state.Flows.Add(RefreshTokenFlow);
        }

        if (permissions.Contains(LegacyLogoutPermission) || permissions.Contains(OpenIddictConstants.Permissions.Endpoints.EndSession))
        {
            state.Endpoints.Add(LogoutEndpoint);
        }

        if (permissions.Contains(OpenIddictConstants.Permissions.Endpoints.Introspection))
        {
            state.Endpoints.Add(IntrospectionEndpoint);
        }

        if (permissions.Contains(OpenIddictConstants.Permissions.Endpoints.Revocation))
        {
            state.Endpoints.Add(RevocationEndpoint);
        }

        return state;
    }

    /// <summary>
    /// Whether the application is a confidential client, which signs in with a client secret.
    /// </summary>
    /// <param name="state">The fields of the application.</param>
    /// <returns><see langword="true"/> when the application is a confidential client.</returns>
    public static bool IsConfidential(OpenIdApplicationState state)
        => string.Equals(state.Type, OpenIddictConstants.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds what a tool returns about one application, from an explicit list of fields.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="state">The fields of the application.</param>
    /// <returns>The fields the tool returns.</returns>
    public static object Describe(IServiceProvider services, OpenIdApplicationState state)
    {
        return new
        {
            id = state.Id,
            clientId = state.ClientId,
            displayName = state.DisplayName,
            type = state.Type,

            // A confidential client always has a secret. Only its hash is stored, and no tool reads it.
            hasClientSecret = IsConfidential(state),
            consentType = state.ConsentType,
            redirectUris = state.RedirectUris,
            postLogoutRedirectUris = state.PostLogoutRedirectUris,
            allowedFlows = state.Flows,
            allowedEndpoints = state.Endpoints,
            requirePkce = state.RequirePkce,
            requirePushedAuthorizationRequests = state.RequirePushedAuthorizationRequests,
            roles = state.Roles,
            scopes = state.Scopes,
            adminUrl = GetApplicationUrl(services, state.Id),
        };
    }

    /// <summary>
    /// Gets the address of the Edit page of an application.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The ID the admin page uses for the application.</param>
    /// <returns>The address, or <see langword="null"/> when the request cannot build one.</returns>
    public static string GetApplicationUrl(IServiceProvider services, string id)
    {
        return AgentToolLinks.GetAdminPath(services, OpenIdConstants.Features.Core, "Application", "Edit", new RouteValueDictionary
        {
            { "id", id },
        });
    }

    /// <summary>
    /// Writes names for a sentence: each in quotes, separated by commas.
    /// </summary>
    /// <param name="names">The names.</param>
    /// <returns>The names in quotes.</returns>
    public static string Quote(IEnumerable<string> names)
        => string.Join(", ", names.Select(name => $"'{name}'"));
}
