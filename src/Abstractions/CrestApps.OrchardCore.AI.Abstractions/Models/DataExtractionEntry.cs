namespace CrestApps.OrchardCore.AI.Models;

public sealed class DataExtractionEntry
{
    /// <summary>
    /// Gets or sets the unique key for this extraction entry.
    /// Must be alphanumeric with underscores only.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a description of what to extract.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether to keep extracting values across messages.
    /// </summary>
    public bool AllowMultipleValues { get; set; }

    /// <summary>
    /// Gets or sets whether to allow replacing a single-value field.
    /// </summary>
    public bool IsUpdatable { get; set; }
}
