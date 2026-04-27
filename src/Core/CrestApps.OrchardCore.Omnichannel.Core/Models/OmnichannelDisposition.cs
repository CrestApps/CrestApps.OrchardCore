using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel disposition.
/// </summary>
public sealed class OmnichannelDisposition : CatalogItem, IDisplayTextAwareModel, ICloneable<OmnichannelDisposition>
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
    /// Gets or sets the capture date.
    /// </summary>
    public bool CaptureDate { get; set; }

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
    /// Creates a copy of the current disposition.
    /// </summary>
    public OmnichannelDisposition Clone()
    {
        return new OmnichannelDisposition
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            Description = Description,
            CaptureDate = CaptureDate,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
