using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using CrestApps.Core.AI.Documents.Models;

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
    /// Gets or sets the document retrieval mode options.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> RetrievalModes { get; set; }
}
