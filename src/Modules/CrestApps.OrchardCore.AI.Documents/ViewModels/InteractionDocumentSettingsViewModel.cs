using CrestApps.Core.AI.Documents.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for interaction document settings.
/// </summary>
public class InteractionDocumentSettingsViewModel
{
    /// <summary>
    /// Gets or sets the index profile name.
    /// </summary>
    public string IndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the index profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> IndexProfiles { get; set; }

    /// <summary>
    /// Gets or sets the default document retrieval mode.
    /// </summary>
    public DocumentRetrievalMode RetrievalMode { get; set; } = DocumentRetrievalMode.Chunk;

    /// <summary>
    /// Gets or sets a value indicating whether document uploads are allowed for chat interactions.
    /// </summary>
    public bool AllowDocumentUploads { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether image uploads are allowed for chat interactions.
    /// </summary>
    public bool AllowImageUploads { get; set; }

    /// <summary>
    /// Gets or sets how much extracted text a single uploaded document may hold and still be indexed.
    /// Zero means no limit.
    /// </summary>
    public int MaxIndexableCharacters { get; set; } = 50000;

    /// <summary>
    /// Gets or sets a value indicating whether figures inside uploaded documents are described by a
    /// vision model.
    /// </summary>
    public bool DescribeFiguresInUploads { get; set; } = true;

    /// <summary>
    /// Gets or sets the document retrieval mode options.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> RetrievalModes { get; set; }
}
