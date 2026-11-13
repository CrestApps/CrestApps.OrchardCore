using CrestApps.OrchardCore.Attachments.Core.Indexes;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Attachments.Core.Services;

/// <summary>
/// Stores attachment records.
/// </summary>
public sealed class AttachmentStore : DocumentCatalog<Attachment, AttachmentIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AttachmentStore(ISession session)
        : base(session)
    {
        CollectionName = AttachmentsConstants.CollectionName;
    }

    /// <summary>
    /// Lists the attachments of a record, newest first.
    /// </summary>
    /// <param name="owner">The record.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attachments.</returns>
    public async Task<IReadOnlyList<Attachment>> ListByOwnerAsync(RecordRef owner, CancellationToken cancellationToken = default)
    {
        if (owner.IsEmpty)
        {
            return [];
        }

        var kind = owner.Kind;
        var id = owner.Id;
        var attachments = await Session.Query<Attachment, AttachmentIndex>(index => index.OwnerKind == kind && index.OwnerId == id, collection: CollectionName)
            .OrderByDescending(index => index.UploadedUtc)
            .ListAsync(cancellationToken);

        return attachments.ToArray();
    }
}
