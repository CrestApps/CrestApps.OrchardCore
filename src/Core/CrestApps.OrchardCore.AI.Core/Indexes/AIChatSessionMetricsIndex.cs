using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

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

    /// <summary>
    /// The hour of day (0-23) when the session started, for time-of-day reporting.
    /// </summary>
    public int HourOfDay { get; set; }

    /// <summary>
    /// The day of week (0=Sunday, 6=Saturday) when the session started.
    /// </summary>
    public int DayOfWeek { get; set; }

    public int TotalInputTokens { get; set; }

    public int TotalOutputTokens { get; set; }

    public double AverageResponseLatencyMs { get; set; }

    public bool? UserRating { get; set; }

    public DateTime CreatedUtc { get; set; }
}
