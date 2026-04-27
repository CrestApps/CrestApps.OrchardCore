namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for model and system performance metrics.
/// </summary>
public class ChatAnalyticsPerformanceViewModel
{
    /// <summary>
    /// Gets or sets the average response latency ms.
    /// </summary>
    public double AverageResponseLatencyMs { get; set; }

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
    /// Gets or sets the average tokens per session.
    /// </summary>
    public double AverageTokensPerSession { get; set; }

    /// <summary>
    /// Gets or sets the average input tokens per session.
    /// </summary>
    public double AverageInputTokensPerSession { get; set; }

    /// <summary>
    /// Gets or sets the average output tokens per session.
    /// </summary>
    public double AverageOutputTokensPerSession { get; set; }

    /// <summary>
    /// Gets or sets the sessions with token data.
    /// </summary>
    public int SessionsWithTokenData { get; set; }

    /// <summary>
    /// Gets or sets the sessions with latency data.
    /// </summary>
    public int SessionsWithLatencyData { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has data.
    /// </summary>
    public bool HasData { get; set; }
}
