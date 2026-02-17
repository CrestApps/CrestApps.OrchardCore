using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Documents.ViewModels;

public class EditChatInteractionDocumentsViewModel
{
    public string ItemId { get; set; }

    public IList<ChatInteractionDocumentInfo> Documents { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of top matching document chunks to include in AI context.
    /// </summary>
    public int TopN { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether an index profile is configured for document embedding.
    /// </summary>
    public bool HasIndexProfile { get; set; }

    /// <summary>
    /// Gets or sets the name of the configured index profile, if any.
    /// </summary>
    public string IndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets whether the configured index profile has a valid embedding search service.
    /// </summary>
    public bool HasVectorSearchService { get; set; }
}
