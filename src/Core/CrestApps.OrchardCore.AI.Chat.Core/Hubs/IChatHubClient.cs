using CrestApps.OrchardCore.AI.Models;

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

    Task ReceiveConversationAssistantToken(string identifier, string messageId, string token, string responseId, AssistantMessageAppearance appearance = null);

    Task ReceiveConversationAssistantComplete(string identifier, string messageId);

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
