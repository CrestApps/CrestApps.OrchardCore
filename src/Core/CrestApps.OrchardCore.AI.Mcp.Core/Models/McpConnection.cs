using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

public sealed class McpConnection : Model, IDisplayTextAwareModel
{
    public string DisplayText { get; set; }

    public string TransportType { get; set; }

    public string Location { get; set; }

    public Dictionary<string, string> TransportOptions { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public McpConnection Clone()
    {
        return new McpConnection()
        {
            Id = Id,
            DisplayText = DisplayText,
            TransportType = TransportType,
            Location = Location,
            TransportOptions = TransportOptions,
            CreatedUtc = CreatedUtc,
            Author = Author,
        };
    }
}
