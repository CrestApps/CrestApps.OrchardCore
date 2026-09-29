using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// Reads and writes the pictures kept on a message.
/// </summary>
public static class MessagingAttachmentExtensions
{
    /// <summary>
    /// Gets the pictures of a message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The message's pictures; empty when it has none.</returns>
    public static IReadOnlyList<MessagingAttachment> GetAttachments(this OmnichannelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.TryGet<MessagingMessageAttachments>(out var attachments) && attachments.Items is not null
            ? attachments.Items.Where(item => !string.IsNullOrEmpty(item?.Id)).ToArray()
            : [];
    }

    /// <summary>
    /// Gets how many media items of a received message could not be kept.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The number of media items that were announced but not stored.</returns>
    public static int GetSkippedAttachmentCount(this OmnichannelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.TryGet<MessagingMessageAttachments>(out var attachments)
            ? Math.Max(0, attachments.SkippedCount)
            : 0;
    }

    /// <summary>
    /// Records the pictures of a message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="attachments">The pictures, in the order they were sent.</param>
    public static void SetAttachments(this OmnichannelMessage message, IEnumerable<MessagingAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(message);

        var part = message.GetOrCreate<MessagingMessageAttachments>();
        part.Items = attachments?.Where(item => !string.IsNullOrEmpty(item?.Id)).ToList() ?? [];

        message.Put(part);
    }
}
