using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class PromptRoutingContext
{
    public string Prompt { get; set; }

    public string Source { get; set; }

    public string ConnectionName { get; set; }

    public object CompletionResource { get; }

    public IList<ChatInteractionDocumentInfo> Documents { get; set; } = [];

    public IList<ChatMessage> ConversationHistory { get; set; } = [];

    public int? MaxHistoryMessagesForImageGeneration { get; set; }

    /// <summary>
    /// Gets or sets the pre-intent capability resolution result. When populated,
    /// the intent detector can use these capability summaries to make more informed
    /// routing decisions about external capabilities.
    /// </summary>
    public PreIntentResolutionContext PreIntentResolution { get; set; }

    public PromptRoutingContext(object completionResource)
    {
        ArgumentNullException.ThrowIfNull(completionResource);

        CompletionResource = completionResource;
    }
}
