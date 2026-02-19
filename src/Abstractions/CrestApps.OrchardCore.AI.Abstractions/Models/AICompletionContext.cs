namespace CrestApps.OrchardCore.AI.Models;

public class AICompletionContext
{
    public string ConnectionName { get; set; }

    public bool UserMarkdownInResponse { get; set; } = true;

    public bool DisableTools { get; set; }

    public string SystemMessage { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public int? PastMessagesCount { get; set; }

    public bool UseCaching { get; set; } = true;

    public string[] ToolNames { get; set; }

    public string[] McpConnectionIds { get; set; }

    public string DataSourceId { get; set; }

    public string DeploymentId { get; set; }

    public Dictionary<string, object> AdditionalProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
}
