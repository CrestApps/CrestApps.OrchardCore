namespace CrestApps.Core.AI.Chat.Handlers;

/// <summary>
/// Shared item keys used by chat session handlers to exchange processing results.
/// </summary>
public static class AIChatSessionHandlerContextKeys
{
    public const string DataExtractionChangeSet = "ai-chat:data-extraction-change-set";
    public const string PostCloseProcessingResult = "ai-chat:post-close-processing-result";
}
