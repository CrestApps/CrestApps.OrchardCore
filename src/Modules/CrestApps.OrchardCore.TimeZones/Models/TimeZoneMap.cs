using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.TimeZones.Models;

/// <summary>
/// Represents a named time zone map entry.
/// </summary>
public sealed class TimeZoneMap : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<TimeZoneMap>
{
    /// <summary>
    /// Gets or sets the unique friendly name shown to editors.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Orchard Core time zone identifier stored for this entry.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the created UTC timestamp.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the modified UTC timestamp.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author user name.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the current time zone map.
    /// </summary>
    public TimeZoneMap Clone()
    {
        return new TimeZoneMap
        {
            ItemId = ItemId,
            Name = Name,
            TimeZoneId = TimeZoneId,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
