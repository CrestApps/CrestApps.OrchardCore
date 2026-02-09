using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class PromptRoutingRequest
{
    public string Prompt { get; set; }

    public ChatInteraction Interaction { get; set; }

    public IList<ChatMessage> ConversationHistory { get; set; } = [];

    public int? MaxHistoryMessagesForImageGeneration { get; set; }

    public CancellationToken CancellationToken { get; set; }
}
