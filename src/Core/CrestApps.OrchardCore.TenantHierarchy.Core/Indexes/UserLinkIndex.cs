using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Indexes the <see cref="UserLink"/> documents of a child tenant.
/// </summary>
public sealed class UserLinkIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the local user identifier.
    /// </summary>
    public string ChildUserId { get; set; }

    /// <summary>
    /// Gets or sets the parent tenant identifier.
    /// </summary>
    public string ParentTenantId { get; set; }

    /// <summary>
    /// Gets or sets the parent user identifier.
    /// </summary>
    public string ParentUserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link is current.
    /// </summary>
    public bool IsCurrent { get; set; }
}
