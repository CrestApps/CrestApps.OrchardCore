using CrestApps.OrchardCore.Attachments.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Attachments.Core.Indexes;

/// <summary>
/// Maps attachments to <see cref="AttachmentIndex"/>.
/// </summary>
public sealed class AttachmentIndexProvider : IndexProvider<Attachment>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentIndexProvider"/> class.
    /// </summary>
    public AttachmentIndexProvider()
    {
        CollectionName = AttachmentsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Attachment> context)
    {
        context.For<AttachmentIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(attachment => new AttachmentIndex
            {
                ItemId = attachment.ItemId,
                OwnerKind = attachment.Owner.Kind,
                OwnerId = attachment.Owner.Id,
                ContentType = attachment.ContentType,
                Length = attachment.Length,
                UploadedUtc = attachment.UploadedUtc,
            });
    }
}
