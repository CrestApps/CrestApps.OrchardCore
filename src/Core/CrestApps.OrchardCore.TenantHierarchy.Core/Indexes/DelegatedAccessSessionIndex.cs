using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Indexes the <see cref="DelegatedAccessSession"/> documents of a parent tenant.
/// </summary>
public sealed class DelegatedAccessSessionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the hash of the session identifier.
    /// </summary>
    public string SessionHash { get; set; }

    /// <summary>
    /// Gets or sets the parent user.
    /// </summary>
    public string ParentUserId { get; set; }

    /// <summary>
    /// Gets or sets the parent sign-in that started the session.
    /// </summary>
    public string ParentSessionId { get; set; }

    /// <summary>
    /// Gets or sets the registry entry of the child tenant.
    /// </summary>
    public string ChildEntryId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the session has not ended.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets when the session started.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
