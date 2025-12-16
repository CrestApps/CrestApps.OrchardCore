using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Chat.Models;

public sealed class CustomChatPart : ContentPart
{
    public string CustomChatInstanceId { get; set; }


    public string SessionId { get; set; }


    public string UserId { get; set; }


    public string Source { get; set; }


    public string ProviderName { get; set; }


    public string ConnectionName { get; set; }


    public string DeploymentId { get; set; }


    public string Title { get; set; }


    public string SystemMessage { get; set; }


    public int MaxTokens { get; set; }


    public float Temperature { get; set; }


    public float TopP { get; set; }


    public float FrequencyPenalty { get; set; }


    public float PresencePenalty { get; set; }


    public int PastMessagesCount { get; set; }


    public bool UseCaching { get; set; }


    public bool IsCustomInstance { get; set; }


    public string[] ToolNames { get; set; } = [];


    public DateTime CreatedUtc { get; set; }
}
