using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Documents.ViewModels;

public class ChatInteractionDocumentsViewModel
{
    /// <summary>
    /// Gets or sets the unique identifier of the interaction.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the collection of chat documents associated with the current context.
    /// </summary>
    public IList<ChatDocumentInfo> Documents { get; set; } = [];

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
