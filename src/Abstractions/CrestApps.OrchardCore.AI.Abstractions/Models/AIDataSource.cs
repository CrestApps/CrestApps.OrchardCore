using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIDataSource : Model, IDisplayTextAwareModel
{
    public string ProfileSource { get; set; }

    public string Type { get; set; }

    public string DisplayText { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public AIDataSource Clone()
    {
        return new AIDataSource
        {
            Id = Id,
            DisplayText = DisplayText,
            ProfileSource = ProfileSource,
            Type = Type,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties.Clone(),
        };
    }
}
