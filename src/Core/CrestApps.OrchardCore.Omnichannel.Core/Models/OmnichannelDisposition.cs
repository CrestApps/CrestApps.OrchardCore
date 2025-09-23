using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelDisposition : CatalogItem, IDisplayTextAwareModel, ICloneable<OmnichannelDisposition>
{
    public string DisplayText { get; set; }

    public string Description { get; set; }

    public bool CaptureDate { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public OmnichannelDisposition Clone()
    {
        return new OmnichannelDisposition()
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
