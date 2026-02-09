using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class PromptRoutingContext
{
    public string Prompt { get; set; }

    public string Source { get; set; }

    public string ConnectionName { get; set; }

    public AICompletionContext CompletionContext { get; set; }

    public IList<ChatInteractionDocumentInfo> Documents { get; set; } = [];

    public IList<ChatMessage> ConversationHistory { get; set; } = [];

    public int? MaxHistoryMessagesForImageGeneration { get; set; }
}
