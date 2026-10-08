using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Keeps the files an agent attached to a reply, after holding them to what the conversation's channel can carry:
/// its formats, its number of files and its total size. A file is matched to a format by its own bytes wherever the
/// format has a signature, never by the name or type the browser gave it.
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
    /// Checks and stores the uploaded files.
    /// </summary>
    /// <param name="channel">The channel the reply is sent on.</param>
    /// <param name="files">The uploaded files.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The stored files, or the reason they were refused, in which case nothing was stored.</returns>
    public async Task<(IList<MessagingAttachment> Attachments, string Refusal)> StoreAsync(IMessagingChannel channel, IFormFileCollection files, CancellationToken cancellationToken = default)
    {
        var uploads = files?.Where(file => file.Length > 0).ToArray() ?? [];

        if (uploads.Length == 0)
        {
            return ([], null);
        }

        var capabilities = channel?.Capabilities.Attachments;

        if (capabilities is null || !capabilities.IsSupported)
        {
            return (null, S["{0} cannot carry attachments.", channel?.DisplayName.Value ?? string.Empty].Value);
        }

        if (uploads.Length > capabilities.MaxCount)
        {
            return (null, S["A message can carry at most {0} attachments.", capabilities.MaxCount].Value);
        }

        if (uploads.Sum(file => file.Length) > capabilities.MaxTotalBytes)
        {
            return (null, S["The attachments are too large to send together. The limit is {0} KB.", capabilities.MaxTotalBytes / 1024].Value);
        }

        // Every file is checked before any is stored, so a refusal never leaves the ones before it behind.
        var accepted = new List<(MessagingAttachment Attachment, byte[] Bytes)>();

        foreach (var file in uploads)
        {
            using var buffer = new MemoryStream();

            await file.CopyToAsync(buffer, cancellationToken);

            var bytes = buffer.ToArray();
            var fileName = Path.GetFileName(file.FileName);
            var format = MessagingFileFormats.Detect(bytes, fileName, file.ContentType, capabilities.Formats);

            if (format is null)
            {
                _logger.LogWarning(
                    "Refused an attachment the {Channel} channel does not carry (declared as {ContentType}).",
                    channel.Name,
                    file.ContentType.SanitizeLogValue());

                return (null, S["{0} cannot be sent. {1} accepts: {2}.", fileName, channel.DisplayName.Value, string.Join(", ", capabilities.Formats.Select(item => item.Name))].Value);
            }

            accepted.Add((new MessagingAttachment
            {
                Id = "out-" + UniqueId.GenerateId(),
                ContentType = format.ContentType,
                FileName = fileName,
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
                "Stored {Count} attachment(s) ({Bytes} bytes) for an outbound {Channel} message.",
                accepted.Count,
                accepted.Sum(item => item.Attachment.Length),
                channel.Name);
        }

        return (accepted.Select(item => item.Attachment).ToList(), null);
    }

    /// <summary>
    /// Deletes files that no message ended up referring to.
    /// </summary>
    /// <param name="attachments">The stored files.</param>
    public async Task DeleteAsync(IEnumerable<MessagingAttachment> attachments)
    {
        foreach (var attachment in attachments ?? [])
        {
            await _attachmentStore.DeleteAsync(attachment.Id);
        }
    }
}
