using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Keeps the pictures an agent attached to a reply, after holding them to what the conversation's channel can carry.
/// A picture is recognised by its own bytes, never by the name or type the browser gave it.
/// </summary>
public sealed class MessagingAttachmentUploads
{
    private readonly IMessagingAttachmentStore _attachmentStore;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingAttachmentUploads"/> class.
    /// </summary>
    public MessagingAttachmentUploads(
        IMessagingAttachmentStore attachmentStore,
        ILogger<MessagingAttachmentUploads> logger,
        IStringLocalizer<MessagingAttachmentUploads> stringLocalizer)
    {
        _attachmentStore = attachmentStore;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks and stores the uploaded pictures.
    /// </summary>
    /// <param name="channel">The channel the reply is sent on.</param>
    /// <param name="files">The uploaded files.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The stored pictures, or the reason they were refused, in which case nothing was stored.</returns>
    public async Task<(IList<MessagingAttachment> Attachments, string Refusal)> StoreAsync(IMessagingChannel channel, IFormFileCollection files, CancellationToken cancellationToken = default)
    {
        var uploads = files?.Where(file => file.Length > 0).ToArray() ?? [];

        if (uploads.Length == 0)
        {
            return ([], null);
        }

        if (channel is null || !channel.Capabilities.SupportsMedia)
        {
            return (null, S["This channel cannot carry pictures."].Value);
        }

        var capabilities = channel.Capabilities;

        if (uploads.Length > capabilities.MaxMediaCount)
        {
            return (null, S["A message can carry at most {0} pictures.", capabilities.MaxMediaCount].Value);
        }

        if (uploads.Sum(file => file.Length) > capabilities.MaxMediaBytes)
        {
            return (null, S["The pictures are too large to send together. The limit is {0} KB.", capabilities.MaxMediaBytes / 1024].Value);
        }

        // Every picture is checked before any is stored, so a refusal never leaves the ones before it behind.
        var accepted = new List<(MessagingAttachment Attachment, byte[] Bytes)>();

        foreach (var file in uploads)
        {
            using var buffer = new MemoryStream();

            await file.CopyToAsync(buffer, cancellationToken);

            var bytes = buffer.ToArray();

            if (!MessagingImageFormat.TryDetect(bytes, out var contentType))
            {
                _logger.LogWarning("Refused an attachment that is not a supported picture (declared as {ContentType}).", file.ContentType.SanitizeLogValue());

                return (null, S["{0} is not a JPEG, PNG, GIF or WebP picture.", Path.GetFileName(file.FileName)].Value);
            }

            accepted.Add((new MessagingAttachment
            {
                Id = "out-" + UniqueId.GenerateId(),
                ContentType = contentType,
                FileName = Path.GetFileName(file.FileName),
                Length = bytes.Length,
            }, bytes));
        }

        foreach (var (attachment, bytes) in accepted)
        {
            await _attachmentStore.StoreAsync(attachment.Id, bytes, cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Stored {Count} picture(s) ({Bytes} bytes) for an outbound {Channel} message.",
                accepted.Count,
                accepted.Sum(item => item.Attachment.Length),
                channel.Name);
        }

        return (accepted.Select(item => item.Attachment).ToList(), null);
    }

    /// <summary>
    /// Deletes pictures that no message ended up referring to.
    /// </summary>
    /// <param name="attachments">The stored pictures.</param>
    public async Task DeleteAsync(IEnumerable<MessagingAttachment> attachments)
    {
        foreach (var attachment in attachments ?? [])
        {
            await _attachmentStore.DeleteAsync(attachment.Id);
        }
    }
}
