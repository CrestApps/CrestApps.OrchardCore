using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of time-off policies.
/// </summary>
public sealed class TimeOffPolicyIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the policy name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets how the policy accrues.
    /// </summary>
    public TimeOffAccrualMethod AccrualMethod { get; set; }

    /// <summary>
    /// Gets or sets whether the policy is in use.
    /// </summary>
    public bool Enabled { get; set; }
}
