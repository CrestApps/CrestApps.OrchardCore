using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of portal inbox items.
/// </summary>
public sealed class WorkforceInboxIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets whether the worker read it.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was sent.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
