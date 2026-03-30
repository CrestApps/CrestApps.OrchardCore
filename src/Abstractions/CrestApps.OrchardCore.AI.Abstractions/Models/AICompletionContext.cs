using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.AI.Models;

public class AICompletionContext
{
    public string ConnectionName { get; set; }

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

    public string[] AgentNames { get; set; }

    public string[] McpConnectionIds { get; set; }

    public string[] A2AConnectionIds { get; set; }

    public string DataSourceId { get; set; }

    public string ChatDeploymentName { get; set; }

    public string UtilityDeploymentName { get; set; }

    [JsonInclude]
    [JsonPropertyName("DeploymentId")]
    private string _deploymentIdBackingField
    {
        set => ChatDeploymentName = value;
    }

    [JsonInclude]
    [JsonPropertyName("ChatDeploymentId")]
    private string _chatDeploymentIdBackingField
    {
        set => ChatDeploymentName = value;
    }

    [JsonInclude]
    [JsonPropertyName("UtilityDeploymentId")]
    private string _utilityDeploymentIdBackingField
    {
        set => UtilityDeploymentName = value;
    }

    public Dictionary<string, object> AdditionalProperties { get; } = new(StringComparer.OrdinalIgnoreCase);
}
