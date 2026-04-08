using CrestApps.Core.AI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.AI.Chat.Services;

/// <summary>
/// Default implementation of <see cref="IChatNotificationSender"/> that dispatches
/// notifications to the appropriate <see cref="IChatNotificationTransport"/>
/// resolved via keyed service lookup using <see cref="ChatContextType"/>.
/// </summary>
public sealed class DefaultChatNotificationSender : IChatNotificationSender
{
    private readonly IServiceProvider _serviceProvider;

    public DefaultChatNotificationSender(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task SendAsync(string sessionId, ChatContextType chatType, ChatNotification notification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(notification);

        var transport = GetTransport(chatType);

        return transport.SendNotificationAsync(sessionId, notification);
    }

    public Task UpdateAsync(string sessionId, ChatContextType chatType, ChatNotification notification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(notification);

        var transport = GetTransport(chatType);

        return transport.UpdateNotificationAsync(sessionId, notification);
    }

    public Task RemoveAsync(string sessionId, ChatContextType chatType, string notificationType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationType);

        var transport = GetTransport(chatType);

        return transport.RemoveNotificationAsync(sessionId, notificationType);
    }

    private IChatNotificationTransport GetTransport(ChatContextType chatType)
    {
        return _serviceProvider.GetKeyedService<IChatNotificationTransport>(chatType)
            ?? throw new InvalidOperationException(
                $"No {nameof(IChatNotificationTransport)} is registered for chat type '{chatType}'. " +
                $"Ensure the module that provides this chat type is enabled and registers its transport.");
    }
}
