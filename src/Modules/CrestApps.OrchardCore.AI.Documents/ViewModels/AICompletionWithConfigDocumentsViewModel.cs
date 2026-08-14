using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Documents.ViewModels;

/// <summary>
/// Represents the view model used to manage uploaded knowledgebase documents for the
/// AI Completion using Direct Config workflow activity.
/// </summary>
public class AICompletionWithConfigDocumentsViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the embedded interaction the documents are keyed to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the documents currently attached to the activity.
    /// </summary>
    public IList<ChatDocumentInfo> Documents { get; set; } = [];

    /// <summary>
    /// Gets or sets the files uploaded for the activity.
    /// </summary>
    public IFormFile[] Files { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of documents to remove.
    /// </summary>
    public string[] RemovedDocumentIds { get; set; }

    /// <summary>
    /// Gets or sets the number of top matching document chunks to include in the AI context.
    /// </summary>
    public int TopN { get; set; } = 3;

    /// <summary>
    /// Gets or sets the document retrieval mode override for the activity.
    /// </summary>
    public DocumentRetrievalMode? DocumentRetrievalMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an index profile is configured for document embedding.
    /// </summary>
    public bool HasIndexProfile { get; set; }

    /// <summary>
    /// Gets or sets the name of the configured index profile, if any.
    /// </summary>
    public string IndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the configured index profile has a valid embedding search service.
    /// </summary>
    public bool HasVectorSearchService { get; set; }

    /// <summary>
    /// Gets or sets the available document retrieval modes.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> DocumentRetrievalModes { get; set; } = [];
}
