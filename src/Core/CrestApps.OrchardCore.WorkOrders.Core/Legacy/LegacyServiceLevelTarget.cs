using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// A target of a <see cref="LegacyServiceLevelPolicy"/>: the response and resolution minutes of one priority.
/// </summary>
internal sealed class LegacyServiceLevelTarget
{
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public WorkOrderPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the minutes to respond.
    /// </summary>
    public int ResponseMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes to resolve.
    /// </summary>
    public int ResolveMinutes { get; set; }
}
