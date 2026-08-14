using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Core.Hubs;

/// <summary>
/// Defines the common SignalR client methods shared by both AI Chat and Chat Interaction hubs.
/// </summary>
public interface IChatHubClient
{
    /// <summary>
    /// Receives an error message for the active chat session.
    /// </summary>
    /// <param name="error">The error message to display.</param>
    Task ReceiveError(string error);

    /// <summary>
    /// Receives a transcript update for the specified audio interaction.
    /// </summary>
    /// <param name="identifier">The identifier of the transcript stream.</param>
    /// <param name="text">The transcript text.</param>
    /// <param name="isFinal">Whether the transcript segment is final.</param>
    Task ReceiveTranscript(string identifier, string text, bool isFinal);

    /// <summary>
    /// Receives an audio chunk for the specified response stream.
    /// </summary>
    /// <param name="identifier">The identifier of the audio stream.</param>
    /// <param name="base64Audio">The base64-encoded audio payload.</param>
    /// <param name="contentType">The audio content type.</param>
    Task ReceiveAudioChunk(string identifier, string base64Audio, string contentType);

    /// <summary>
    /// Receives a signal that audio streaming has completed for the specified response.
    /// </summary>
    /// <param name="identifier">The identifier of the completed audio stream.</param>
    Task ReceiveAudioComplete(string identifier);

    /// <summary>
    /// Receives the user's message while a conversation-mode exchange is in progress.
    /// </summary>
    /// <param name="identifier">The identifier of the conversation stream.</param>
    /// <param name="text">The user message text.</param>
    Task ReceiveConversationUserMessage(string identifier, string text);

    /// <summary>
    /// Receives an assistant token for the active conversation response.
    /// </summary>
    /// <param name="identifier">The identifier of the conversation stream.</param>
    /// <param name="messageId">The assistant message identifier.</param>
    /// <param name="token">The token to append.</param>
    /// <param name="responseId">The response identifier.</param>
    /// <param name="appearance">The assistant appearance metadata.</param>
    Task ReceiveConversationAssistantToken(string identifier, string messageId, string token, string responseId, AssistantMessageAppearance appearance = null);

    /// <summary>
    /// Receives a signal that the assistant has completed the specified conversation response.
    /// </summary>
    /// <param name="identifier">The identifier of the conversation stream.</param>
    /// <param name="messageId">The completed assistant message identifier.</param>
    Task ReceiveConversationAssistantComplete(string identifier, string messageId);

    /// <summary>
    /// Sends a notification system message to the client. If a notification with the same
    /// type already exists, it is replaced.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    Task ReceiveNotification(ChatNotification notification);

    /// <summary>
    /// Updates an existing notification on the client. Only replaces the notification
    /// if one with a matching type exists.
    /// </summary>
    /// <param name="notification">The notification update to apply.</param>
    Task UpdateNotification(ChatNotification notification);

    /// <summary>
    /// Removes a notification from the client by its type.
    /// </summary>
    /// <param name="notificationType">The notification type to remove.</param>
    Task RemoveNotification(string notificationType);
}
