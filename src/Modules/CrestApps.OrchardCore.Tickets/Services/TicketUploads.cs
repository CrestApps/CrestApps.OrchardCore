using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Stores the files posted with a reply or a new ticket as files of the timeline entry about to be added, under the
/// site's attachment policy, for the agents' pages and the requesters' portal alike.
/// </summary>
public sealed class TicketUploads
{
    private readonly IEnumerable<IAttachmentService> _attachments;

    internal readonly IStringLocalizer S;

    public TicketUploads(IEnumerable<IAttachmentService> attachments, IStringLocalizer<TicketUploads> stringLocalizer)
    {
        _attachments = attachments;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets whether files can be attached (the Attachments feature is on).
    /// </summary>
    public bool CanAttach
        => _attachments.Any();

    /// <summary>
    /// Stores the files as files of an entry.
    /// </summary>
    /// <param name="files">The posted files; empty ones are skipped.</param>
    /// <param name="entryId">The id of the entry about to be added.</param>
    /// <param name="uploadedBy">Who sent them.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The stored ids, or the reasons the files were refused (then nothing is kept).</returns>
    public async Task<(List<string> Attached, IReadOnlyList<string> Errors)> StoreAsync(IReadOnlyList<IFormFile> files, string entryId, PartyRef uploadedBy, CancellationToken cancellationToken)
    {
        var uploads = files?.Where(file => file is { Length: > 0 }).ToList() ?? [];

        if (uploads.Count == 0)
        {
            return ([], []);
        }

        if (_attachments.FirstOrDefault() is not { } attachments)
        {
            return ([], [S["Files can't be attached here."]]);
        }

        var owner = new RecordRef(TimelineConstants.AttachmentOwnerKind, entryId);
        var read = new List<AttachmentUpload>(uploads.Count);

        foreach (var file in uploads)
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);

            read.Add(new AttachmentUpload
            {
                Owner = owner,
                FileName = file.FileName,
                DeclaredContentType = file.ContentType,
                Content = buffer.ToArray(),
                UploadedBy = uploadedBy,
            });
        }

        var stored = await attachments.AddAsync(read, cancellationToken);

        return stored.Succeeded
            ? ([.. stored.Attachments.Select(attachment => attachment.ItemId)], [])
            : ([], stored.Errors);
    }

    /// <summary>
    /// Deletes files stored for an entry that wasn't added after all.
    /// </summary>
    /// <param name="attachmentIds">The ids.</param>
    /// <returns>A task.</returns>
    public async Task DeleteAsync(IReadOnlyList<string> attachmentIds)
    {
        if (attachmentIds is not { Count: > 0 } || _attachments.FirstOrDefault() is not { } attachments)
        {
            return;
        }

        foreach (var attachmentId in attachmentIds)
        {
            await attachments.DeleteAsync(attachmentId, CancellationToken.None);
        }
    }
}
