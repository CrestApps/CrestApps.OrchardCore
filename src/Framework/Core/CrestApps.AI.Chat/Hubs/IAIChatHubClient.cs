using CrestApps.AI.Models;

namespace CrestApps.AI.Chat.Hubs;

/// <summary>
/// Defines the SignalR client methods that the AI Chat hub can invoke on connected clients.
/// Covers text chat, conversation mode (STT/TTS), and notification system messages.
/// </summary>
public interface IAIChatHubClient
{
    Task ReceiveError(string error);

    Task LoadSession(object data);

    Task MessageRated(string messageId, bool? userRating);

    // Conversation mode (speech-to-text / text-to-speech).

    Task ReceiveTranscript(string identifier, string text, bool isFinal);

    Task ReceiveAudioChunk(string identifier, string base64Audio, string contentType);

    Task ReceiveAudioComplete(string identifier);

    Task ReceiveConversationUserMessage(string identifier, string text);

    Task ReceiveConversationAssistantToken(string identifier, string messageId, string token, string responseId);

    Task ReceiveConversationAssistantComplete(string identifier, string messageId);

    // Notification system messages.

    /// <summary>
    /// Sends a notification system message to the client. If a notification with the same
    /// type already exists, it is replaced.
    /// </summary>
    Task ReceiveNotification(ChatNotification notification);

    /// <summary>
    /// Updates an existing notification on the client. Only replaces the notification
    /// if one with a matching type exists.
    /// </summary>
    Task UpdateNotification(ChatNotification notification);

    /// <summary>
    /// Removes a notification from the client by its type.
    /// </summary>
    Task RemoveNotification(string notificationType);
}
