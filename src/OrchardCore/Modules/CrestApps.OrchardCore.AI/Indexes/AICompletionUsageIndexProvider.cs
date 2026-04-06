using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Indexes;

internal sealed class AICompletionUsageIndexProvider : IndexProvider<AICompletionUsageRecord>
{
    public AICompletionUsageIndexProvider()
    {
        CollectionName = AIConstants.AICollectionName;
    }

    public override void Describe(DescribeContext<AICompletionUsageRecord> context)
    {
        context.For<AICompletionUsageIndex>()
            .Map(record => new AICompletionUsageIndex
            {
                ContextType = record.ContextType,
                SessionId = record.SessionId,
                ProfileId = record.ProfileId,
                InteractionId = record.InteractionId,
                UserId = record.UserId,
                UserName = record.UserName,
                VisitorId = record.VisitorId,
                ClientId = record.ClientId,
                IsAuthenticated = record.IsAuthenticated,
                ProviderName = record.ProviderName,
                ClientName = record.ClientName,
                ConnectionName = record.ConnectionName,
                DeploymentName = record.DeploymentName,
                ModelName = record.ModelName,
                ResponseId = record.ResponseId,
                IsStreaming = record.IsStreaming,
                InputTokenCount = record.InputTokenCount,
                OutputTokenCount = record.OutputTokenCount,
                TotalTokenCount = record.TotalTokenCount,
                ResponseLatencyMs = record.ResponseLatencyMs,
                CreatedUtc = record.CreatedUtc,
            });
    }
}
