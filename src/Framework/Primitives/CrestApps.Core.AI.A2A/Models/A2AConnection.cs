using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.A2A.Models;

public sealed class A2AConnection : CatalogItem, IDisplayTextAwareModel, ICloneable<A2AConnection>
{
    public string DisplayText { get; set; }

    public string Endpoint { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public A2AConnection Clone()
    {
        return new A2AConnection()
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            Endpoint = Endpoint,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties,
        };
    }
}
