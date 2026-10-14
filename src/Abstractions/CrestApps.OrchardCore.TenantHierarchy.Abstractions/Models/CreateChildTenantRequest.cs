namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Holds what a parent administrator enters to create a child tenant. It never carries a host, a prefix or database
/// settings: those come from the parent policy only.
/// </summary>
public sealed class CreateChildTenantRequest
{
    /// <summary>
    /// Gets or sets the display name of the child tenant.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the slug of the child tenant. It becomes the first label of its host.
    /// </summary>
    public string Slug { get; set; }

    /// <summary>
    /// Gets or sets the description of the child tenant.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the setup recipe.
    /// </summary>
    public string RecipeName { get; set; }

    /// <summary>
    /// Gets or sets the parent user who creates the child tenant.
    /// </summary>
    public string ActorUserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the parent user who creates the child tenant.
    /// </summary>
    public string ActorUserName { get; set; }
}
