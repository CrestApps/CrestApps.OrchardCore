using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelCampaign : CatalogEntry, IDisplayTextAwareModel, ICloneable<OmnichannelCampaign>
{
    public string DisplayText { get; set; }

    public string Descriptions { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public IList<string> DispositionIds { get; set; }

    public OmnichannelCampaign Clone()
    {
        return new OmnichannelCampaign()
        {
            Id = Id,
            DisplayText = DisplayText,
            Descriptions = Descriptions,
            CreatedUtc = CreatedUtc,
            Author = Author,
            DispositionIds = DispositionIds,
            OwnerId = OwnerId,
        };
    }
}

