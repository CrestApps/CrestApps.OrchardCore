using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;

namespace CrestApps.OrchardCore.Timeline.Services;

/// <summary>
/// Deletes the files attached to a timeline entry when the entry is deleted with its record: the files belong to it.
/// </summary>
internal sealed class TimelineEntryFilesCleanup : ITimelineEntryHandler
{
    private readonly IAttachmentService _attachments;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineEntryFilesCleanup"/> class.
    /// </summary>
    /// <param name="attachments">The attachment service.</param>
    public TimelineEntryFilesCleanup(IAttachmentService attachments)
    {
        _attachments = attachments;
    }

    /// <inheritdoc/>
    public async Task DeletedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        foreach (var attachmentId in entry?.AttachmentIds ?? [])
        {
            await _attachments.DeleteAsync(attachmentId, cancellationToken);
        }
    }
}
