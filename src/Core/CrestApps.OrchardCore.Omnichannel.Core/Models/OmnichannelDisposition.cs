using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelDisposition : CatalogEntry, IDisplayTextAwareModel, ICloneable<OmnichannelDisposition>
{
    public string DisplayText { get; set; }

    public string Descriptions { get; set; }

    public bool CaptureDate { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public OmnichannelDisposition Clone()
    {
        return new OmnichannelDisposition()
        {
            Id = Id,
            DisplayText = DisplayText,
            Descriptions = Descriptions,
            CaptureDate = CaptureDate,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}

