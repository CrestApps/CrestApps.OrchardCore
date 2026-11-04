using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach.Core.Indexes;

/// <summary>
/// The index outreach runs are found by.
/// </summary>
public sealed class OutreachRunIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the kind of record the run sends for.
    /// </summary>
    public string OwnerType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record the run sends for.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the state of the run.
    /// </summary>
    public OutreachRunStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the run was started.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
