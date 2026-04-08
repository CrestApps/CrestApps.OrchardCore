using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat.Hubs;

/// <summary>
/// Defines the SignalR client methods that the AI Chat hub can invoke on connected clients.
/// Covers text chat, conversation mode (STT/TTS), and notification system messages.
/// </summary>
public interface IAIChatHubClient
{
    /// <summary>
    /// Sends an error message to the client.
    /// </summary>
    /// <param name="error">The error message text.</param>
    Task ReceiveError(string error);

    /// <summary>
    /// Loads session data on the client, replacing the current chat state.
    /// </summary>
    /// <param name="data">The serialized session data.</param>
    Task LoadSession(object data);

    /// <summary>
    /// Notifies the client that a message rating has been updated.
    /// </summary>
    /// <param name="messageId">The identifier of the rated message.</param>
    /// <param name="userRating">The user's rating value, or <see langword="null"/> if the rating was cleared.</param>
    Task MessageRated(string messageId, bool? userRating);

    // Conversation mode (speech-to-text / text-to-speech).

    /// <summary>
    /// Sends a speech-to-text transcript fragment to the client.
    /// </summary>
    /// <param name="identifier">The conversation turn identifier.</param>
    /// <param name="text">The transcribed text.</param>
    /// <param name="isFinal">Whether this is the final transcript for the utterance.</param>
    Task ReceiveTranscript(string identifier, string text, bool isFinal);

    /// <summary>
    /// Sends a chunk of synthesized audio data to the client for playback.
    /// </summary>
    /// <param name="identifier">The conversation turn identifier.</param>
    /// <param name="base64Audio">The Base64-encoded audio data.</param>
    /// <param name="contentType">The MIME type of the audio (e.g., "audio/wav").</param>
    Task ReceiveAudioChunk(string identifier, string base64Audio, string contentType);

    /// <summary>
    /// Notifies the client that audio streaming for a conversation turn is complete.
    /// </summary>
    /// <param name="identifier">The conversation turn identifier.</param>
    Task ReceiveAudioComplete(string identifier);

    /// <summary>
    /// Sends the finalized user message text in conversation mode.
    /// </summary>
    /// <param name="identifier">The conversation turn identifier.</param>
    /// <param name="text">The user's final message text.</param>
    Task ReceiveConversationUserMessage(string identifier, string text);

    /// <summary>
    /// Sends a single token of the assistant's response in conversation mode.
    /// </summary>
    /// <param name="identifier">The conversation turn identifier.</param>
    /// <param name="messageId">The assistant message identifier.</param>
    /// <param name="token">The response token text.</param>
    /// <param name="responseId">The response identifier for grouping tokens.</param>
    Task ReceiveConversationAssistantToken(string identifier, string messageId, string token, string responseId);

    /// <summary>
    /// Notifies the client that the assistant response in conversation mode is complete.
    /// </summary>
    /// <param name="identifier">The conversation turn identifier.</param>
    /// <param name="messageId">The assistant message identifier.</param>
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
