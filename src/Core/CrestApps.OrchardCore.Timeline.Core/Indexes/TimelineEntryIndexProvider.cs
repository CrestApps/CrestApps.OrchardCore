using CrestApps.OrchardCore.Timeline.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Timeline.Core.Indexes;

/// <summary>
/// Maps timeline entries to <see cref="TimelineEntryIndex"/>.
/// </summary>
public sealed class TimelineEntryIndexProvider : IndexProvider<TimelineEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineEntryIndexProvider"/> class.
    /// </summary>
    public TimelineEntryIndexProvider()
    {
        CollectionName = TimelineConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TimelineEntry> context)
    {
        context.For<TimelineEntryIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(entry => new TimelineEntryIndex
            {
                ItemId = entry.ItemId,
                SubjectKind = entry.Subject.Kind,
                SubjectId = entry.Subject.Id,
                Kind = entry.Kind,
                Visibility = entry.Visibility,
                AuthorKind = entry.Author.Kind,
                AuthorId = entry.Author.Id,
                Channel = entry.Channel,
                ExternalId = entry.ExternalId,
                CreatedUtc = entry.CreatedUtc,
                Redacted = entry.RedactedUtc is not null,
            });

        // One row per file, so a record's files are listed without reading its entries.
        context.For<TimelineAttachmentIndex>()
            .ExclusiveToDocumentType()
            .Map(entry => (entry.AttachmentIds ?? [])
                .Where(attachmentId => !string.IsNullOrEmpty(attachmentId))
                .Distinct(StringComparer.Ordinal)
                .Select(attachmentId => new TimelineAttachmentIndex
                {
                    AttachmentId = attachmentId,
                    EntryId = entry.ItemId,
                    SubjectKind = entry.Subject.Kind,
                    SubjectId = entry.Subject.Id,
                    Kind = entry.Kind,
                    Visibility = entry.Visibility,
                    AuthorName = entry.AuthorName,
                    CreatedUtc = entry.CreatedUtc,
                    Redacted = entry.RedactedUtc is not null,
                }));
    }
}
