using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IExternalChatRelayEventHandler"/> that routes
/// relay events to the <see cref="IChatNotificationSender"/> for typing indicators,
/// agent-connected notifications, wait-time updates, connection-status indicators,
/// and session-ended bubbles.
/// </summary>
internal sealed class DefaultExternalChatRelayEventHandler : IExternalChatRelayEventHandler
{
    private readonly IChatNotificationSender _notifications;
    private readonly IStringLocalizer _localizer;
    private readonly ILogger _logger;

    public DefaultExternalChatRelayEventHandler(
        IChatNotificationSender notifications,
        IStringLocalizer<DefaultExternalChatRelayEventHandler> localizer,
        ILogger<DefaultExternalChatRelayEventHandler> logger)
    {
        _notifications = notifications;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task HandleEventAsync(
        string sessionId,
        ChatContextType chatType,
        ExternalChatRelayEvent relayEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(relayEvent);

        switch (relayEvent.EventType)
        {
            case ExternalChatRelayEventTypes.AgentTyping:
                await _notifications.ShowTypingAsync(sessionId, chatType, _localizer, relayEvent.AgentName);
                break;

            case ExternalChatRelayEventTypes.AgentStoppedTyping:
                await _notifications.HideTypingAsync(sessionId, chatType);
                break;

            case ExternalChatRelayEventTypes.AgentConnected:
                await _notifications.HideTransferAsync(sessionId, chatType);
                await _notifications.ShowAgentConnectedAsync(
                    sessionId, chatType, _localizer, relayEvent.AgentName, relayEvent.Content);
                break;

            case ExternalChatRelayEventTypes.AgentDisconnected:
                await _notifications.HideAgentConnectedAsync(sessionId, chatType);
                break;

            case ExternalChatRelayEventTypes.AgentReconnecting:
                await _notifications.ShowAgentReconnectingAsync(sessionId, chatType, _localizer, relayEvent.AgentName, relayEvent.Content);
                break;

            case ExternalChatRelayEventTypes.ConnectionLost:
                await _notifications.ShowConnectionLostAsync(sessionId, chatType, _localizer, relayEvent.Content);
                break;

            case ExternalChatRelayEventTypes.ConnectionRestored:
                await _notifications.HideConnectionLostAsync(sessionId, chatType);
                break;

            case ExternalChatRelayEventTypes.WaitTimeUpdated:
                await _notifications.UpdateTransferAsync(
                    sessionId, chatType, _localizer, estimatedWaitTime: relayEvent.Content);
                break;

            case ExternalChatRelayEventTypes.SessionEnded:
                await _notifications.ShowSessionEndedAsync(sessionId, chatType, _localizer, relayEvent.Content);
                break;

            case ExternalChatRelayEventTypes.Message:
                // Message events are not handled by the notification sender.
                // They must be handled by the relay implementation directly using
                // IHubContext to write the message and notify the SignalR group.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Message event for session '{SessionId}' should be handled by the relay implementation.",
                        sessionId);
                }

                break;

            default:
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Unrecognized event type '{EventType}' for session '{SessionId}' is not handled by the default handler.",
                        relayEvent.EventType,
                        sessionId);
                }

                break;
        }
    }
}
