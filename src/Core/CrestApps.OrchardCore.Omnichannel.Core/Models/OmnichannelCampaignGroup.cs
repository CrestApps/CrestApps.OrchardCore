using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents a reporting group that contains one or more omnichannel campaigns.
/// </summary>
public sealed class OmnichannelCampaignGroup : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel, ICloneable<OmnichannelCampaignGroup>
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the created UTC time.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the modified UTC time.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the campaign group.
    /// </summary>
    /// <returns>The cloned campaign group.</returns>
    public OmnichannelCampaignGroup Clone()
    {
        return new OmnichannelCampaignGroup
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            Description = Description,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
