namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for chat extracted data row.
/// </summary>
public class ChatExtractedDataRowViewModel
{
    /// <summary>
    /// Gets or sets the session started utc.
    /// </summary>
    public DateTime SessionStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the values.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
}
