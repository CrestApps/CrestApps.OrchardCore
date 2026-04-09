using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of <see cref="IExternalChatRelayNotificationHandler"/> that
/// processes an <see cref="ExternalChatRelayNotificationResult"/> by removing specified
/// notifications and then sending or updating the notification (if any) via
/// <see cref="IChatNotificationSender"/>. When <see cref="ExternalChatRelayNotificationResult.IsUpdate"/>
/// is <see langword="true"/>, <see cref="IChatNotificationSender.UpdateAsync"/> is used instead
/// of <see cref="IChatNotificationSender.SendAsync"/>.
/// </summary>
internal sealed class DefaultExternalChatRelayNotificationHandler : IExternalChatRelayNotificationHandler
{
    private readonly IChatNotificationSender _sender;

    public DefaultExternalChatRelayNotificationHandler(IChatNotificationSender sender)
    {
        _sender = sender;
    }

    public async Task HandleAsync(
        string sessionId,
        ChatContextType chatType,
        ExternalChatRelayNotificationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var notificationType in result.RemoveNotificationTypes)
        {
            await _sender.RemoveAsync(sessionId, chatType, notificationType);
        }

        if (result.Notification == null)
        {
            return;
        }

        if (result.IsUpdate)
        {
            await _sender.UpdateAsync(sessionId, chatType, result.Notification);

            return;
        }

        await _sender.SendAsync(sessionId, chatType, result.Notification);
    }
}
