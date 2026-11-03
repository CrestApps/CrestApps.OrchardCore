using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;

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

    /// <summary>
    /// Gets or sets the type of value extracted.
    /// </summary>
    public DataExtractionFieldType FieldType { get; set; }

    /// <summary>
    /// Gets or sets the values a choice field allows, one per line.
    /// </summary>
    public string AllowedValues { get; set; }

    /// <summary>
    /// Gets or sets the contact field the value is written to, as <c>PartName.FieldName</c>.
    /// </summary>
    public string ContactField { get; set; }

    /// <summary>
    /// Gets or sets how the value is written to the contact field.
    /// </summary>
    public AIContactFieldMappingMode ContactFieldMode { get; set; }
}
