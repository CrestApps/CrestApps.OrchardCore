namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Holds the favorite and recent child tenants of one parent user, for the tenant picker.
/// </summary>
public sealed class TenantSwitcherPreference
{
    /// <summary>
    /// The number of recent child tenants that are kept.
    /// </summary>
    public const int MaxRecent = 8;

    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the parent user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the registry entries the user marked as favorite.
    /// </summary>
    public List<string> Favorites { get; set; } = [];

    /// <summary>
    /// Gets or sets the registry entries the user entered most recently, most recent first.
    /// </summary>
    public List<string> Recent { get; set; } = [];
}
