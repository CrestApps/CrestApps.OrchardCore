using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Indexes the <see cref="DelegatedAccessCode"/> documents of a parent tenant.
/// </summary>
public sealed class DelegatedAccessCodeIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the hash of the code.
    /// </summary>
    public string CodeHash { get; set; }

    /// <summary>
    /// Gets or sets when the code expires.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }
}
