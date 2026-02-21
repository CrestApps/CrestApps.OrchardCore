namespace CrestApps.OrchardCore.AI.Models;

public class AIProfileDataExtractionSettings
{
    /// <summary>
    /// Gets or sets whether data extraction is enabled for this profile.
    /// </summary>
    public bool EnableDataExtraction { get; set; }

    /// <summary>
    /// Gets or sets the interval at which extraction is performed.
    /// A value of 1 means every message, 2 means every other message, etc.
    /// </summary>
    public int ExtractionCheckInterval { get; set; } = 1;

    /// <summary>
    /// Gets or sets the session inactivity timeout in minutes.
    /// Sessions inactive longer than this duration will be closed by the background task.
    /// </summary>
    public int SessionInactivityTimeoutInMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the list of data extraction entries for this profile.
    /// </summary>
    public List<DataExtractionEntry> DataExtractionEntries { get; set; } = [];
}
