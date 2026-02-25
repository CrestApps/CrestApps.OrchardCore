using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Indexes;

internal sealed class AIChatSessionMetricsIndexProvider : IndexProvider<AIChatSessionEvent>
{
    public AIChatSessionMetricsIndexProvider()
    {
        CollectionName = AIConstants.CollectionName;
    }

    public override void Describe(DescribeContext<AIChatSessionEvent> context)
    {
        context
            .For<AIChatSessionMetricsIndex>()
            .Map(evt =>
            {
                return new AIChatSessionMetricsIndex
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
                    CreatedUtc = evt.CreatedUtc,
                };
            });
    }
}
