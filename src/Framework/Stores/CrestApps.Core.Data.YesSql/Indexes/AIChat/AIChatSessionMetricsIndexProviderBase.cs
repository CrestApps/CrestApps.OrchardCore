using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

public abstract class AIChatSessionMetricsIndexProviderBase : IndexProvider<AIChatSessionEvent>
{
    protected AIChatSessionMetricsIndexProviderBase(string collectionName = null)
    {
        CollectionName = collectionName;
    }

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
                CompletionCount = evt.CompletionCount,
                UserRating = evt.UserRating,
                ThumbsUpCount = evt.ThumbsUpCount,
                ThumbsDownCount = evt.ThumbsDownCount,
                ConversionScore = evt.ConversionScore,
                ConversionMaxScore = evt.ConversionMaxScore,
                CreatedUtc = evt.CreatedUtc,
            });
    }
}
