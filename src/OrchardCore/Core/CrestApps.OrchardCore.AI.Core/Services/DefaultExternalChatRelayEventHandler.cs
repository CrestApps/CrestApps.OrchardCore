using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IExternalChatRelayEventHandler"/> that routes
/// relay events through keyed <see cref="IExternalChatRelayNotificationBuilder"/> services
/// and <see cref="IExternalChatRelayNotificationHandler"/> for extensible notification handling.
/// </summary>
/// <remarks>
/// <para>
/// For each event, this handler resolves an <see cref="IExternalChatRelayNotificationBuilder"/>
/// keyed by <see cref="ExternalChatRelayEvent.EventType"/>. If a builder is found, it creates
/// a <see cref="ChatNotification"/> with <see cref="ChatNotification.Type"/> set from the
/// builder's <see cref="IExternalChatRelayNotificationBuilder.NotificationType"/>, then calls
/// <see cref="IExternalChatRelayNotificationBuilder.Build"/> to populate remaining properties.
/// The result is then processed by the <see cref="IExternalChatRelayNotificationHandler"/>
/// to remove, update, and/or send notifications.
/// </para>
/// <para>
/// To handle custom event types, register a keyed builder:
/// <code>
/// services.AddKeyedScoped&lt;IExternalChatRelayNotificationBuilder, MyBuilder&gt;("my-event-type");
/// </code>
/// </para>
/// </remarks>
internal sealed class DefaultExternalChatRelayEventHandler : IExternalChatRelayEventHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IExternalChatRelayNotificationHandler _notificationHandler;
    private readonly IStringLocalizer T;
    private readonly ILogger _logger;

    public DefaultExternalChatRelayEventHandler(
        IServiceProvider serviceProvider,
        IExternalChatRelayNotificationHandler notificationHandler,
        IStringLocalizer<DefaultExternalChatRelayEventHandler> localizer,
        ILogger<DefaultExternalChatRelayEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _notificationHandler = notificationHandler;
        T = localizer;
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

        var builder = _serviceProvider.GetKeyedService<IExternalChatRelayNotificationBuilder>(relayEvent.EventType);

        if (builder == null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "No notification builder registered for event type '{EventType}' in session '{SessionId}'.",
                    relayEvent.EventType,
                    sessionId);
            }

            return;
        }

        ChatNotification notification = null;

        if (!string.IsNullOrEmpty(builder.NotificationType))
        {
            notification = new ChatNotification(builder.NotificationType);
        }

        var result = new ExternalChatRelayNotificationResult
        {
            Notification = notification,
        };

        builder.Build(relayEvent, notification, result, T);
        await _notificationHandler.HandleAsync(sessionId, chatType, result, cancellationToken);
    }
}
