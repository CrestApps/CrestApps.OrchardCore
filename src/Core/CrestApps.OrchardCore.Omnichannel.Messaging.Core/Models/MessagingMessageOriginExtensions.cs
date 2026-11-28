using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Reads and writes the <see cref="MessagingMessageOrigin"/> kept on a message's property bag.
/// </summary>
public static class MessagingMessageOriginExtensions
{
    /// <summary>
    /// Determines whether a machine wrote the message rather than a person.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> when the message is an automatic one, such as an out-of-office or an auto-reply.</returns>
    public static bool IsAutomatic(this OmnichannelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.TryGet<MessagingMessageOrigin>(out var origin) && origin.IsAutomatic;
    }

    /// <summary>
    /// Records whether a machine wrote the message rather than a person.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="isAutomatic">Whether the message is an automatic one.</param>
    public static void SetAutomatic(this OmnichannelMessage message, bool isAutomatic)
    {
        ArgumentNullException.ThrowIfNull(message);

        // A message a person wrote carries nothing, so most messages are left as they were.
        if (!isAutomatic && !message.TryGet<MessagingMessageOrigin>(out _))
        {
            return;
        }

        message.Put(new MessagingMessageOrigin
        {
            IsAutomatic = isAutomatic,
        });
    }
}
