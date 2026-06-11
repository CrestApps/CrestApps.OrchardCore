namespace CrestApps.OrchardCore.ContentTransfer.Models;

/// <summary>
/// Stores import options collected from the bulk import UI.
/// </summary>
public sealed class ImportContentOptionsPart
{
    /// <summary>
    /// Gets or sets a value indicating whether imported content should be published after it is saved.
    /// </summary>
    public bool PublishImportedContent { get; set; } = true;
}
