namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for AI profile data extraction.
/// </summary>
public class AIProfileDataExtractionViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether enable data extraction.
    /// </summary>
    public bool EnableDataExtraction { get; set; }

    /// <summary>
    /// Gets or sets the extraction check interval.
    /// </summary>
    public int ExtractionCheckInterval { get; set; } = 1;

    /// <summary>
    /// Gets or sets the entries.
    /// </summary>
    public List<DataExtractionEntryViewModel> Entries { get; set; } = [];
}

/// <summary>
/// Represents the view model for data extraction entry.
/// </summary>
public class DataExtractionEntryViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow multiple values.
    /// </summary>
    public bool AllowMultipleValues { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is updatable.
    /// </summary>
    public bool IsUpdatable { get; set; }
}
