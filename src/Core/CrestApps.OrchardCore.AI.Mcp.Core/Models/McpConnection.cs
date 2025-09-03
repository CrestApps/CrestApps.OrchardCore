using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

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
            Id = Id,
            Source = Source,
            DisplayText = DisplayText,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties,
        };
    }
}
