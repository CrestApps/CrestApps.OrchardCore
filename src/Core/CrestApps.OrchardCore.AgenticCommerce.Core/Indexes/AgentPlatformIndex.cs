using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Indexes;

/// <summary>
/// Finds agent platforms by name and by the credentials they sign in with.
/// </summary>
public sealed class AgentPlatformIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the platform's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether its agents may buy.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the hash of its API key.
    /// </summary>
    public string ApiKeyHash { get; set; }

    /// <summary>
    /// Gets or sets its OAuth client ID.
    /// </summary>
    public string OAuthClientId { get; set; }

    /// <summary>
    /// Gets or sets the address of its UCP profile.
    /// </summary>
    public string ProfileUrl { get; set; }
}
