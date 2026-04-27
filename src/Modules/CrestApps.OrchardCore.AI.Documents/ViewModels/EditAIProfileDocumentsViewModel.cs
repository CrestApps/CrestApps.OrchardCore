using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Documents.ViewModels;

/// <summary>
/// Represents the view model for edit AI profile documents.
/// </summary>
public class EditAIProfileDocumentsViewModel
{
    /// <summary>
    /// Gets or sets the profile id.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the documents.
    /// </summary>
    public IList<ChatDocumentInfo> Documents { get; set; } = [];

    /// <summary>
    /// Gets or sets the files uploaded for the profile.
    /// </summary>
    public IFormFile[] Files { get; set; }

    /// <summary>
    /// Gets or sets the IDs of documents to remove.
    /// </summary>
    public string[] RemovedDocumentIds { get; set; }

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
