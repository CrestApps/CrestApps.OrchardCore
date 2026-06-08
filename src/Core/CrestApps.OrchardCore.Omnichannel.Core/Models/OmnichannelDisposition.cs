using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel disposition.
/// </summary>
public sealed class OmnichannelDisposition : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<OmnichannelDisposition>
{
    private string _displayText;

    /// <summary>
    /// Gets or sets the unique disposition name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    [Obsolete("Use the Name property instead.")]
    public string DisplayText
    {
        get => _displayText;
        set
        {
            _displayText = value;

            if (string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(value))
            {
                Name = value;
            }
        }
    }

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
    /// Creates a copy of the current disposition.
    /// </summary>
    public OmnichannelDisposition Clone()
    {
        return new OmnichannelDisposition
        {
            ItemId = ItemId,
            Name = Name,
            Description = Description,
            CaptureDate = CaptureDate,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
