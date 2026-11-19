using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Indexes;

/// <summary>
/// The index row of a service level policy.
/// </summary>
public sealed class ServiceLevelPolicyIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the policy's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the scope the policy is for.
    /// </summary>
    public string Scope { get; set; }
}
