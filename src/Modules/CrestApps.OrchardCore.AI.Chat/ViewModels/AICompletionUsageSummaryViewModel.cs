namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for AI completion usage summary.
/// </summary>
public sealed class AICompletionUsageSummaryViewModel
{
    /// <summary>
    /// Gets or sets the user label.
    /// </summary>
    public string UserLabel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets or sets the client name.
    /// </summary>
    public string ClientName { get; set; }

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string ModelName { get; set; }

    /// <summary>
    /// Gets or sets the total calls.
    /// </summary>
    public int TotalCalls { get; set; }

    /// <summary>
    /// Gets or sets the total sessions.
    /// </summary>
    public int TotalSessions { get; set; }

    /// <summary>
    /// Gets or sets the total chat interactions.
    /// </summary>
    public int TotalChatInteractions { get; set; }

    /// <summary>
    /// Gets or sets the total input tokens.
    /// </summary>
    public long TotalInputTokens { get; set; }

    /// <summary>
    /// Gets or sets the total output tokens.
    /// </summary>
    public long TotalOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the total tokens.
    /// </summary>
    public long TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets the average response latency ms.
    /// </summary>
    public double AverageResponseLatencyMs { get; set; }
}
