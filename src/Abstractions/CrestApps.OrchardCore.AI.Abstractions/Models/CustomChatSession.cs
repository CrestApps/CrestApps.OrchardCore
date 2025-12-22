using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class CustomChatSession : Entity
{
    public string SessionId { get; set; }


    public string CustomChatInstanceId { get; set; }


    public IList<AIChatSessionPrompt> Prompts { get; set; } = [];


    public string Source { get; set; }


    public string UserId { get; set; }


    public string Title { get; set; }


    public DateTime CreatedUtc { get; set; }
}
