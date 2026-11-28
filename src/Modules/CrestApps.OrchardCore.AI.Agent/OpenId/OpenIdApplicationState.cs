namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// What the OpenID tools read from one client application: the fields the Applications admin page edits. It has no
/// client secret; the tools never read one.
/// </summary>
internal sealed class OpenIdApplicationState
{
    /// <summary>
    /// Gets or sets the ID the Applications admin page uses for the application.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the client type: public or confidential.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the consent type.
    /// </summary>
    public string ConsentType { get; set; }

    /// <summary>
    /// Gets or sets the redirect URIs.
    /// </summary>
    public List<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets the post-logout redirect URIs.
    /// </summary>
    public List<string> PostLogoutRedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets the allowed flows, by the names of <see cref="OpenIdToolSupport.Flows"/>.
    /// </summary>
    public List<string> Flows { get; set; } = [];

    /// <summary>
    /// Gets or sets the allowed endpoints, by the names of <see cref="OpenIdToolSupport.Endpoints"/>.
    /// </summary>
    public List<string> Endpoints { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the application must use PKCE.
    /// </summary>
    public bool RequirePkce { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the application must use pushed authorization requests.
    /// </summary>
    public bool RequirePushedAuthorizationRequests { get; set; }

    /// <summary>
    /// Gets or sets the roles the application acts with in the client credentials flow.
    /// </summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the scopes the application may request.
    /// </summary>
    public List<string> Scopes { get; set; } = [];
}
