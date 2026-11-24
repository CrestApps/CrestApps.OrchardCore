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

    /// <summary>
    /// Gets or sets the content types the rows of the import may be, given by
    /// <see cref="IContentImportRowTypeProvider"/>s and kept to those the person who uploaded the file may import.
    /// Empty when every row is the content type of the import.
    /// </summary>
    public string[] RowContentTypes { get; set; } = [];
}
