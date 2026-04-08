using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat.Hubs;

/// <summary>
/// Defines the SignalR client methods that the chat interaction hub can invoke
/// on connected clients, covering error reporting, interaction loading,
/// settings persistence, and history management.
/// </summary>
public interface IChatInteractionHubClient
{
    /// <summary>
    /// Sends an error message to the client.
    /// </summary>
    /// <param name="error">The error message text.</param>
    Task ReceiveError(string error);

    /// <summary>
    /// Loads interaction data on the client, replacing the current state.
    /// </summary>
    /// <param name="data">The serialized interaction data.</param>
    Task LoadInteraction(object data);

    /// <summary>
    /// Notifies the client that interaction settings have been saved.
    /// </summary>
    /// <param name="itemId">The identifier of the content item associated with the interaction.</param>
    /// <param name="title">The updated title of the interaction.</param>
    Task SettingsSaved(string itemId, string title);

    /// <summary>
    /// Notifies the client that the chat history has been cleared.
    /// </summary>
    /// <param name="itemId">The identifier of the content item whose history was cleared.</param>
    Task HistoryCleared(string itemId);

    Task ReceiveTranscript(string identifier, string text, bool isFinal);

    Task ReceiveAudioChunk(string identifier, string base64Audio, string contentType);

    Task ReceiveAudioComplete(string identifier);

    Task ReceiveConversationUserMessage(string identifier, string text);

    Task ReceiveConversationAssistantToken(string identifier, string messageId, string token, string responseId, AssistantMessageAppearance appearance = null);

    Task ReceiveConversationAssistantComplete(string identifier, string messageId);

    Task ReceiveNotification(ChatNotification notification);

    Task UpdateNotification(ChatNotification notification);

    Task RemoveNotification(string notificationType);
}
