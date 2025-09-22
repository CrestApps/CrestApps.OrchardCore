using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIToolInstance : SourceCatalogEntry, IDisplayTextAwareModel, ICloneable<AIToolInstance>
{
    public string DisplayText { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string OwnerId { get; set; }

    public string Author { get; set; }

    public AIToolInstance Clone()
    {
        return new AIToolInstance
        {
            ItemId = ItemId,
            Source = Source,
            DisplayText = DisplayText,
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
        };
    }

    public override string ToString()
    {
        return DisplayText;
    }
}
