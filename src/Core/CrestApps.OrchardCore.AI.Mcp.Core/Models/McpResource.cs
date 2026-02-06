using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Represents an MCP resource entry that wraps the SDK's Resource class and adds catalog metadata.
/// </summary>
public sealed class McpResource : SourceCatalogEntry, IDisplayTextAwareModel, ICloneable<McpResource>
{
    /// <summary>
    /// Gets or sets the UTC date and time when the resource was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the author who created the resource.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who owns this resource.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the display text for this resource.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the MCP SDK Resource instance containing the resource definition.
    /// </summary>
    public Resource Resource { get; set; }

    /// <summary>
    /// Creates a deep copy of this resource entry.
    /// </summary>
    public McpResource Clone()
    {
        var clone = new McpResource()
        {
            ItemId = ItemId,
            Source = Source,
            DisplayText = DisplayText,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties,
        };

        if (Resource != null)
        {
            clone.Resource = new Resource
            {
                Uri = Resource.Uri,
                Name = Resource.Name,
                Title = Resource.Title,
                Description = Resource.Description,
                MimeType = Resource.MimeType,
            };
        }

        return clone;
    }
}
