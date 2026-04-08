using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.Models;

/// <summary>
/// Represents a reusable template. The template holds only generic metadata;
/// source-specific data is stored in <see cref="OrchardCore.Entities.Entity.Properties"/>
/// via metadata classes such as <see cref="ProfileTemplateMetadata"/> or
/// <see cref="SystemPromptTemplateMetadata"/>.
/// </summary>
public sealed class AIProfileTemplate : SourceCatalogEntry, INameAwareModel, IDisplayTextAwareModel, ICloneable<AIProfileTemplate>
{
    /// <summary>
    /// Gets or sets the technical name of the template.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the display text of the template.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description of what this template provides.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the category for grouping templates in the UI.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets whether this template appears in listing UIs.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IsListable { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC timestamp when the template was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owner of this template.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the author of this template.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Creates a deep copy of the current template.
    /// </summary>
    public AIProfileTemplate Clone()
    {
        return new AIProfileTemplate
        {
            ItemId = ItemId,
            Source = Source,
            Name = Name,
            DisplayText = DisplayText,
            Description = Description,
            Category = Category,
            IsListable = IsListable,
            CreatedUtc = CreatedUtc,
            OwnerId = OwnerId,
            Author = Author,
            Properties = new Dictionary<string, object>(Properties),
        };
    }

    public override string ToString()
    {
        if (string.IsNullOrEmpty(DisplayText))
        {
            return Name;
        }

        return DisplayText;
    }
}
