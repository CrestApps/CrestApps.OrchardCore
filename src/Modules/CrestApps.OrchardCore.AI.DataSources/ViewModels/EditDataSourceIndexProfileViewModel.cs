using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// View model for editing Data Source knowledge base index profile embedding settings.
/// </summary>
public class EditDataSourceIndexProfileViewModel
{
    /// <summary>
    /// Gets or sets the selected embedding connection key.
    /// </summary>
    public string EmbeddingConnection { get; set; }

    /// <summary>
    /// Gets or sets the available embedding connections.
    /// </summary>
    public IList<SelectListItem> EmbeddingConnections { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the embedding connection is locked (read-only after creation).
    /// </summary>
    public bool IsLocked { get; set; }
}
