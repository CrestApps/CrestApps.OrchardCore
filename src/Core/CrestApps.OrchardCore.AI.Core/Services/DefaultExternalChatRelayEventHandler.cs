using CrestApps.OrchardCore.AI.Models;
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
/// keyed by <see cref="ExternalChatRelayEvent.EventType"/>. If a builder is found, it builds an
/// <see cref="ExternalChatRelayNotificationResult"/> which is then processed by the
/// <see cref="IExternalChatRelayNotificationHandler"/> to remove and/or send notifications.
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
    private readonly IStringLocalizer _localizer;
    private readonly ILogger _logger;

    public DefaultExternalChatRelayEventHandler(
        IServiceProvider serviceProvider,
        IExternalChatRelayNotificationHandler notificationHandler,
        IStringLocalizer<DefaultExternalChatRelayEventHandler> localizer,
        ILogger<DefaultExternalChatRelayEventHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _notificationHandler = notificationHandler;
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

        var builder = _serviceProvider.GetKeyedService<IExternalChatRelayNotificationBuilder>(relayEvent.EventType);

        if (builder != null)
        {
            var result = builder.Build(relayEvent, _localizer);
            await _notificationHandler.HandleAsync(sessionId, chatType, result, cancellationToken);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "No notification builder registered for event type '{EventType}' in session '{SessionId}'.",
                    relayEvent.EventType,
                    sessionId);
            }
        }
    }
}
