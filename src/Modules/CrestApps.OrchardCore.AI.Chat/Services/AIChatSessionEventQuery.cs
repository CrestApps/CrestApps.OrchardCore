namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Which chat session analytics events to read.
/// </summary>
public sealed class AIChatSessionEventQuery
{
    /// <summary>
    /// Gets or sets the AI profile the sessions belong to, or <see langword="null"/> for every profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the earliest session start, in UTC, or <see langword="null"/> for no lower bound.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest session start, in UTC, or <see langword="null"/> for no upper bound.
    /// </summary>
    public DateTime? ToUtc { get; set; }
}
