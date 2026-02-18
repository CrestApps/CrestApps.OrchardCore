using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class AIDataSource : CatalogItem, IDisplayTextAwareModel, ICloneable<AIDataSource>
{
    [Obsolete("Do no use any more.")]
    public string ProfileSource { get; set; }

    [Obsolete("Do no use any more.")]
    public string Type { get; set; }

    public string DisplayText { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the name of the source index to query for data.
    /// </summary>
    public string SourceIndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the name of the AI knowledge base index used to store document embeddings.
    /// </summary>
    public string AIKnowledgeBaseIndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the source index field name that maps to the document key (reference ID).
    /// When not mapped, the document's native key (_id) is used.
    /// </summary>
    public string KeyFieldName { get; set; }

    /// <summary>
    /// Gets or sets the source index field name that maps to the document title.
    /// </summary>
    public string TitleFieldName { get; set; }

    /// <summary>
    /// Gets or sets the source index field name that maps to the document content (text).
    /// </summary>
    public string ContentFieldName { get; set; }

    public AIDataSource Clone()
    {
        return new AIDataSource
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            CreatedUtc = CreatedUtc,
#pragma warning disable CS0618 // Type or member is obsolete
            ProfileSource = ProfileSource,
            Type = Type,
#pragma warning restore CS0618 // Type or member is obsolete
            Author = Author,
            OwnerId = OwnerId,
            SourceIndexProfileName = SourceIndexProfileName,
            AIKnowledgeBaseIndexProfileName = AIKnowledgeBaseIndexProfileName,
            KeyFieldName = KeyFieldName,
            TitleFieldName = TitleFieldName,
            ContentFieldName = ContentFieldName,
            Properties = Properties.Clone(),
        };
    }
}
