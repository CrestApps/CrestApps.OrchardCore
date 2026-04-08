namespace CrestApps.Core.AI.Models;

public sealed class AICompletionUsageRecord : ExtensibleEntity
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
