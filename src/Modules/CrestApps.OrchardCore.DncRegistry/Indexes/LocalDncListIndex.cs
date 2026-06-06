using YesSql.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;

namespace CrestApps.OrchardCore.DncRegistry.Indexes;

/// <summary>
/// YesSql map index for querying local DNC lists.
/// </summary>
public sealed class LocalDncListIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the unique list identifier.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the display name of the list.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the current import status.
    /// </summary>
    public LocalDncListStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when this list was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
