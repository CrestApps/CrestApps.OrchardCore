namespace CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

public interface IChatInteractionHubClient
{
    Task ReceiveError(string error);

    Task LoadInteraction(object data);

    Task SettingsSaved(string itemId, string title);

    Task HistoryCleared(string itemId);

    Task ReceiveTranscript(string itemId, string text, bool isFinal);

    Task ReceiveAudioChunk(string itemId, string base64Audio, string contentType);

    Task ReceiveAudioComplete(string itemId);

    Task ReceiveConversationUserMessage(string itemId, string text);

    Task ReceiveConversationAssistantToken(string itemId, string messageId, string token, string responseId);

    Task ReceiveConversationAssistantComplete(string itemId, string messageId);
}
