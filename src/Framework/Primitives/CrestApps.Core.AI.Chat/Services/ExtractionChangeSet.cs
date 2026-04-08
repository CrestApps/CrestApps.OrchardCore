namespace CrestApps.Core.AI.Chat.Services;

public sealed class ExtractionChangeSet
{
    public List<ExtractedFieldChange> NewFields { get; set; } = [];

    public bool SessionEnded { get; set; }

    /// <summary>
    /// Gets or sets whether all configured extraction fields have been collected.
    /// </summary>
    public bool AllFieldsCollected { get; set; }
}
