using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// A status a lead can be in, such as <c>Open - Not Contacted</c> or <c>Working - Contacted</c>. Exactly one status
/// is the converted status, which a lead takes when it is converted and which cannot be chosen by hand.
/// </summary>
public sealed class LeadStatus : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<LeadStatus>
{
    /// <summary>
    /// Gets or sets the unique status name. It is fixed once the status is created.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the position of the status in lists, lowest first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets whether a new lead starts in this status.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets whether a lead in this status is closed. Closed leads are not loaded into activities by
    /// default.
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// Gets or sets whether this is the status a lead takes when it is converted.
    /// </summary>
    public bool IsConverted { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the modified utc.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner id.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the current status.
    /// </summary>
    public LeadStatus Clone()
    {
        return new LeadStatus
        {
            ItemId = ItemId,
            Name = Name,
            Description = Description,
            Order = Order,
            IsDefault = IsDefault,
            IsClosed = IsClosed,
            IsConverted = IsConverted,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(Properties)),
        };
    }
}
