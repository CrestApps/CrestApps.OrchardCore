namespace CrestApps.OrchardCore.Tests.Agent.OpenId;

/// <summary>
/// An OpenID client application kept in memory for a tool test.
/// </summary>
internal sealed class FakeOpenIdApplication
{
    public string Id { get; set; }

    public string ClientId { get; set; }

    public string DisplayName { get; set; }

    public string Type { get; set; }

    public string ConsentType { get; set; }

    /// <summary>
    /// Gets or sets what the store keeps of the client secret. No tool may return or change it.
    /// </summary>
    public string ClientSecret { get; set; }

    public List<string> Permissions { get; set; } = [];

    public List<string> Requirements { get; set; } = [];

    public List<string> RedirectUris { get; set; } = [];

    public List<string> PostLogoutRedirectUris { get; set; } = [];

    public List<string> Roles { get; set; } = [];
}
