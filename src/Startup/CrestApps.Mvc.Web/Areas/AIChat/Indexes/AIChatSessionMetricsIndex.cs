using CrestApps.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Areas.AIChat.Indexes;

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

    public bool? UserRating { get; set; }

    public int ThumbsUpCount { get; set; }

    public int ThumbsDownCount { get; set; }

    public int? ConversionScore { get; set; }

    public int? ConversionMaxScore { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class AIChatSessionMetricsIndexProvider : IndexProvider<AIChatSessionEvent>
{
    public override void Describe(DescribeContext<AIChatSessionEvent> context)
    {
        context.For<AIChatSessionMetricsIndex>()
            .Map(evt => new AIChatSessionMetricsIndex
            {
                SessionId = evt.SessionId,
                ProfileId = evt.ProfileId,
                VisitorId = evt.VisitorId,
                UserId = evt.UserId,
                IsAuthenticated = evt.IsAuthenticated,
                SessionStartedUtc = evt.SessionStartedUtc,
                SessionEndedUtc = evt.SessionEndedUtc,
                MessageCount = evt.MessageCount,
                HandleTimeSeconds = evt.HandleTimeSeconds,
                IsResolved = evt.IsResolved,
                HourOfDay = evt.SessionStartedUtc.Hour,
                DayOfWeek = (int)evt.SessionStartedUtc.DayOfWeek,
                TotalInputTokens = evt.TotalInputTokens,
                TotalOutputTokens = evt.TotalOutputTokens,
                AverageResponseLatencyMs = evt.AverageResponseLatencyMs,
                UserRating = evt.UserRating,
                ThumbsUpCount = evt.ThumbsUpCount,
                ThumbsDownCount = evt.ThumbsDownCount,
                ConversionScore = evt.ConversionScore,
                ConversionMaxScore = evt.ConversionMaxScore,
                CreatedUtc = evt.CreatedUtc,
            });
    }
}
