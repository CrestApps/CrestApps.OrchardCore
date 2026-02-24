namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// View model for model and system performance metrics.
/// </summary>
public class ChatAnalyticsPerformanceViewModel
{
    public double AverageResponseLatencyMs { get; set; }

    public long TotalInputTokens { get; set; }

    public long TotalOutputTokens { get; set; }

    public long TotalTokens { get; set; }

    public double AverageTokensPerSession { get; set; }

    public double AverageInputTokensPerSession { get; set; }

    public double AverageOutputTokensPerSession { get; set; }

    public int SessionsWithTokenData { get; set; }

    public int SessionsWithLatencyData { get; set; }

    public bool HasData { get; set; }
}
