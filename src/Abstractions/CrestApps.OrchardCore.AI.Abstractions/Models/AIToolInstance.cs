using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIToolInstance : SourceModel, IDisplayTextAwareModel
{
    public string DisplayText { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string OwnerId { get; set; }

    public string Author { get; set; }

    public AIToolInstance Clone()
    {
        return new AIToolInstance
        {
            Id = Id,
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
