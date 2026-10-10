using CrestApps.OrchardCore.TenantHierarchy.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;

/// <summary>
/// Indexes the <see cref="TenantSwitcherPreference"/> documents of a parent tenant.
/// </summary>
public sealed class TenantSwitcherPreferenceIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the parent user.
    /// </summary>
    public string UserId { get; set; }
}
