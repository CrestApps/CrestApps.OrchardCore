using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Indexes;

public sealed class AICompletionUsageIndex : MapIndex
{
    public string ContextType { get; set; }

    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public string InteractionId { get; set; }

    public string UserId { get; set; }

    public string UserName { get; set; }

    public string VisitorId { get; set; }

    public string ClientId { get; set; }

    public bool IsAuthenticated { get; set; }

    public string ProviderName { get; set; }

    public string ClientName { get; set; }

    public string ConnectionName { get; set; }

    public string DeploymentName { get; set; }

    public string ModelName { get; set; }

    public string ResponseId { get; set; }

    public bool IsStreaming { get; set; }

    public int InputTokenCount { get; set; }

    public int OutputTokenCount { get; set; }

    public int TotalTokenCount { get; set; }

    public double ResponseLatencyMs { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class AICompletionUsageIndexProvider : IndexProvider<AICompletionUsageRecord>
{
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
