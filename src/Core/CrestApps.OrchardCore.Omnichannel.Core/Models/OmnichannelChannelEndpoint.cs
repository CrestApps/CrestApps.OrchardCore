using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel channel endpoint.
/// </summary>
public sealed class OmnichannelChannelEndpoint : CatalogItem, IDisplayTextAwareModel, ICloneable<OmnichannelChannelEndpoint>
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner id.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the current channel endpoint.
    /// </summary>
    public OmnichannelChannelEndpoint Clone()
    {
        return new OmnichannelChannelEndpoint
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            Channel = Channel,
            Value = Value,
            Description = Description,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
