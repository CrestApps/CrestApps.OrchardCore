using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Indexes the <see cref="HierarchyAuditEvent"/> documents of a parent tenant.
/// </summary>
public sealed class HierarchyAuditEventIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the event name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the registry entry of the child tenant.
    /// </summary>
    public string ChildEntryId { get; set; }

    /// <summary>
    /// Gets or sets the parent user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets when the event happened.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
