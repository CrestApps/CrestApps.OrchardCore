namespace CrestApps.OrchardCore.AI.Chat.Hubs;

public interface IAIChatHubClient
{
    Task ReceiveError(string error);

    Task LoadSession(object data);

    Task MessageRated(string messageId, bool? userRating);

    Task ReceiveTranscript(string sessionId, string text, bool isFinal);

    Task ReceiveAudioChunk(string sessionId, string base64Audio, string contentType);

    Task ReceiveAudioComplete(string sessionId);

    Task ReceiveConversationUserMessage(string sessionId, string text);

    Task ReceiveConversationAssistantToken(string sessionId, string messageId, string token, string responseId);

    Task ReceiveConversationAssistantComplete(string sessionId, string messageId);
}
