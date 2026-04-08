using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

public sealed class AIChatSessionMetricsIndex : MapIndex
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public string VisitorId { get; set; }

    public string UserId { get; set; }

    public bool IsAuthenticated { get; set; }

    public DateTime SessionStartedUtc { get; set; }

    public DateTime? SessionEndedUtc { get; set; }

    public int MessageCount { get; set; }

    public double HandleTimeSeconds { get; set; }

    public bool IsResolved { get; set; }

    public int HourOfDay { get; set; }

    public int DayOfWeek { get; set; }

    public int TotalInputTokens { get; set; }

    public int TotalOutputTokens { get; set; }

    public double AverageResponseLatencyMs { get; set; }

    public int CompletionCount { get; set; }

    public bool? UserRating { get; set; }

    public int ThumbsUpCount { get; set; }

    public int ThumbsDownCount { get; set; }

    public int? ConversionScore { get; set; }

    public int? ConversionMaxScore { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class AIChatSessionMetricsIndexProvider : AIChatSessionMetricsIndexProviderBase
{
}
