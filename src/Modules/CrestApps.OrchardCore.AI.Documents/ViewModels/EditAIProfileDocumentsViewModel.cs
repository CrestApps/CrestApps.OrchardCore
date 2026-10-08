using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

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
    /// Gets or sets the document retrieval mode override for the profile or template.
    /// </summary>
    public DocumentRetrievalMode? DocumentRetrievalMode { get; set; }

    /// <summary>
    /// Gets or sets how much extracted text an uploaded document may hold and still be indexed, or
    /// <see langword="null"/> to use the site's own limit.
    /// </summary>
    public int? MaxIndexableCharacters { get; set; }

    /// <summary>
    /// Gets or sets whether figures in an uploaded document are described by a vision model, or
    /// <see langword="null"/> to follow the site's own setting.
    /// </summary>
    /// <remarks>
    /// The form posts <see cref="DescribeFiguresInUploadsSelection"/>, which is the only thing that sets
    /// this. Binding both would let whichever the binder reached last decide the value.
    /// </remarks>
    [BindNever]
    public bool? DescribeFiguresInUploads { get; set; }

    /// <summary>
    /// Gets or sets <see cref="DescribeFiguresInUploads"/> as the string a three-state select posts.
    /// </summary>
    /// <remarks>
    /// Binding the nullable bool straight to a select with an empty option renders a null as "false" in
    /// some binders, which preselects "Do not describe figures" on a brand new profile instead of the
    /// site default. Going through a string keeps the three states distinct whatever the binder does.
    /// </remarks>
    public string DescribeFiguresInUploadsSelection
    {
        get => DescribeFiguresInUploads switch
        {
            true => "true",
            false => "false",
            _ => string.Empty,
        };

        set => DescribeFiguresInUploads = value switch
        {
            "true" => true,
            "false" => false,
            _ => null,
        };
    }

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

    /// <summary>
    /// Gets or sets whether the attached documents can be downloaded. Only profile documents are
    /// downloadable; profile template documents use a reference type the download endpoint does not serve.
    /// </summary>
    [BindNever]
    public bool AllowDownload { get; set; }

    /// <summary>
    /// Gets or sets the available document retrieval modes.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> DocumentRetrievalModes { get; set; } = [];
}
