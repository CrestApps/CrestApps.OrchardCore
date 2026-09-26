using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// Represents the index over <see cref="Models.Cadence"/> for listing and lookup.
/// </summary>
public sealed class CadenceIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the display text (name) of the schedule.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the schedule is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the schedule was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
