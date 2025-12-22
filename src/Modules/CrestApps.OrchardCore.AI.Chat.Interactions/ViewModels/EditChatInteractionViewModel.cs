namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

public class EditChatInteractionViewModel
{
    public string InteractionId { get; set; }

    public string Title { get; set; }

    public string DeploymentId { get; set; }

    public string ConnectionName { get; set; }

    public string SystemMessage { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public int? PastMessagesCount { get; set; }

    public string[] ToolNames { get; set; }

    public string[] ToolInstanceIds { get; set; }

    public string[] McpConnectionIds { get; set; }

    public bool IsNew { get; set; }
}
