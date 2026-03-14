namespace CrestApps.OrchardCore.AI.Chat.Core.Hubs;

/// <summary>
/// Defines the common SignalR client methods shared by both AI Chat and Chat Interaction hubs.
/// </summary>
public interface IChatHubClient
{
    Task ReceiveError(string error);

    Task ReceiveTranscript(string identifier, string text, bool isFinal);

    Task ReceiveAudioChunk(string identifier, string base64Audio, string contentType);

    Task ReceiveAudioComplete(string identifier);

    Task ReceiveConversationUserMessage(string identifier, string text);

    Task ReceiveConversationAssistantToken(string identifier, string messageId, string token, string responseId);

    Task ReceiveConversationAssistantComplete(string identifier, string messageId);
}
