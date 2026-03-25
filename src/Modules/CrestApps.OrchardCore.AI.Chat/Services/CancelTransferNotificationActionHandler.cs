using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Handles the "cancel-transfer" notification action.
/// Resets the session's <see cref="AIChatSession.ResponseHandlerName"/> or
/// <see cref="ChatInteraction.ResponseHandlerName"/> back to <see langword="null"/>,
/// which causes subsequent prompts to be routed to the default AI handler.
/// Also removes the transfer notification from the UI.
/// </summary>
internal sealed class CancelTransferNotificationActionHandler : IChatNotificationActionHandler
{
    public async Task HandleAsync(ChatNotificationActionContext context, CancellationToken cancellationToken)
    {
        var logger = context.Services.GetRequiredService<ILogger<CancelTransferNotificationActionHandler>>();
        var notificationSender = context.Services.GetRequiredService<IChatNotificationSender>();

        if (context.ChatType == ChatContextType.AIChatSession)
        {
            var sessionManager = context.Services.GetRequiredService<IAIChatSessionManager>();
            var session = await sessionManager.FindByIdAsync(context.SessionId);

            if (session is null)
            {
                logger.LogWarning("Cancel transfer failed: session '{SessionId}' not found.", context.SessionId);
                return;
            }

            session.ResponseHandlerName = null;
            await sessionManager.SaveAsync(session);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Cancelled transfer for session '{SessionId}'. Handler reset to AI.", context.SessionId);
            }
        }
        else if (context.ChatType == ChatContextType.ChatInteraction)
        {
            var interactionManager = context.Services.GetRequiredService<ICatalogManager<ChatInteraction>>();
            var interaction = await interactionManager.FindByIdAsync(context.SessionId);

            if (interaction is null)
            {
                logger.LogWarning("Cancel transfer failed: interaction '{SessionId}' not found.", context.SessionId);
                return;
            }

            interaction.ResponseHandlerName = null;
            await interactionManager.UpdateAsync(interaction);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Cancelled transfer for interaction '{SessionId}'. Handler reset to AI.", context.SessionId);
            }
        }

        // Remove the transfer notification from the UI.
        await notificationSender.RemoveAsync(
            context.SessionId,
            context.ChatType,
            ChatNotificationTypes.Transfer);
    }
}
