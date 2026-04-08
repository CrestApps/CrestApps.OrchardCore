using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Chat.Services;

/// <summary>
/// Handles the "end-session" notification action.
/// Closes the chat session and sends a "session ended" notification to the UI.
/// </summary>
public sealed class EndSessionNotificationActionHandler : IChatNotificationActionHandler
{
    public async Task HandleAsync(ChatNotificationActionContext context, CancellationToken cancellationToken)
    {
        var logger = context.Services.GetRequiredService<ILogger<EndSessionNotificationActionHandler>>();
        var notificationSender = context.Services.GetRequiredService<IChatNotificationSender>();
        var T = context.Services.GetRequiredService<IStringLocalizer<EndSessionNotificationActionHandler>>();

        if (context.ChatType == ChatContextType.AIChatSession)
        {
            var sessionManager = context.Services.GetRequiredService<IAIChatSessionManager>();
            var session = await sessionManager.FindByIdAsync(context.SessionId);

            if (session is null)
            {
                logger.LogWarning("End session failed: session '{SessionId}' not found.", context.SessionId);

                return;
            }

            var timeProvider = context.Services.GetService<TimeProvider>() ?? TimeProvider.System;

            session.Status = ChatSessionStatus.Closed;
            session.ClosedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await sessionManager.SaveAsync(session);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Session '{SessionId}' ended via notification action.", context.SessionId);
            }
        }

        // Show a "session ended" notification.
        await notificationSender.SendAsync(
            context.SessionId,
            context.ChatType,
            new ChatNotification(ChatNotificationTypes.SessionEnded)
            {
                Content = T["This chat session has ended."].Value,
                Icon = "fa-solid fa-circle-check",
                Dismissible = true,
            });
    }
}
