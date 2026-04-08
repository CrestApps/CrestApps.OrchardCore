using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.Mcp.Models;

public sealed class McpConnection : SourceCatalogEntry, IDisplayTextAwareModel, ICloneable<McpConnection>
{
    public string DisplayText { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public McpConnection Clone()
    {
        return new McpConnection()
        {
            ItemId = ItemId,
            Source = Source,
            DisplayText = DisplayText,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties,
        };
    }
}
