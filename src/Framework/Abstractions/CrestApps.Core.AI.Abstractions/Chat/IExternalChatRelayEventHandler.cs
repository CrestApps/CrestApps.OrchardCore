using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Handles events received from an <see cref="IExternalChatRelay"/>. Implementations
/// map relay events to chat notifications, message writes, and other actions.
/// </summary>
/// <remarks>
/// The default implementation resolves a keyed <see cref="IExternalChatRelayNotificationBuilder"/>
/// by <see cref="ExternalChatRelayEvent.EventType"/> and delegates to
/// <see cref="IExternalChatRelayNotificationHandler"/> to send/remove notifications.
/// To handle custom event types, register a keyed builder:
/// <code>
/// services.AddKeyedScoped&lt;IExternalChatRelayNotificationBuilder, MyBuilder&gt;("my-event-type");
/// </code>
/// </remarks>
public interface IExternalChatRelayEventHandler
{
    /// <summary>
    /// Processes an event received from the external relay.
    /// </summary>
    /// <param name="sessionId">The session or interaction identifier.</param>
    /// <param name="chatType">The type of chat context.</param>
    /// <param name="relayEvent">The event received from the external system.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task HandleEventAsync(
        string sessionId,
        ChatContextType chatType,
        ExternalChatRelayEvent relayEvent,
        CancellationToken cancellationToken = default);
}
