using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public sealed class McpConnection : Model, IDisplayTextAwareModel
{
    public string DisplayText { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public McpConnection Clone()
    {
        return new McpConnection()
        {
            Id = Id,
            DisplayText = DisplayText,
            CreatedUtc = CreatedUtc,
            Author = Author,
        };
    }
}
