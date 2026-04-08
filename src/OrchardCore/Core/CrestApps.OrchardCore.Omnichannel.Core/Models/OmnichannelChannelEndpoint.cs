using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.Core;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelChannelEndpoint : CatalogItem, IDisplayTextAwareModel, ICloneable<OmnichannelChannelEndpoint>
{
    public string DisplayText { get; set; }

    public string Channel { get; set; }

    public string Value { get; set; }

    public string Description { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public OmnichannelChannelEndpoint Clone()
    {
        return new OmnichannelChannelEndpoint()
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
