using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Indexes the <see cref="ChildTenantEntry"/> documents of a parent tenant.
/// </summary>
public sealed class ChildTenantEntryIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the registry entry identifier.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier of the child tenant.
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// Gets or sets the tenant name of the child tenant.
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// Gets or sets the slug of the child tenant.
    /// </summary>
    public string Slug { get; set; }

    /// <summary>
    /// Gets or sets the display name of the child tenant.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the life cycle status of the child tenant.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets when the entry was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a child tenant pending removal is removed.
    /// </summary>
    public DateTime? RetainUntilUtc { get; set; }
}
