namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for chat conversion goals row.
/// </summary>
public class ChatConversionGoalsRowViewModel
{
    /// <summary>
    /// Gets or sets the session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the session started utc.
    /// </summary>
    public DateTime SessionStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the total points.
    /// </summary>
    public string TotalPoints { get; set; }

    /// <summary>
    /// Gets or sets the values.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
