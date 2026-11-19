using CrestApps.OrchardCore.Attachments.Core.Indexes;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

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
    /// <summary>
    /// Lists, oldest first, a page of the attachments of records of some kinds uploaded before a time.
    /// </summary>
    /// <param name="ownerKinds">The kinds of the records.</param>
    /// <param name="uploadedBeforeUtc">The time they were uploaded before.</param>
    /// <param name="uploadedFromUtc">The upload time to start at (included), or <see langword="null"/>.</param>
    /// <param name="take">How many at most.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attachments.</returns>
    public async Task<IReadOnlyList<Attachment>> ListUploadedBeforeAsync(IReadOnlyCollection<string> ownerKinds, DateTime uploadedBeforeUtc, DateTime? uploadedFromUtc, int take, CancellationToken cancellationToken = default)
    {
        var kinds = (ownerKinds ?? []).Where(kind => !string.IsNullOrEmpty(kind)).Distinct(StringComparer.Ordinal).ToArray();

        if (kinds.Length == 0 || take <= 0)
        {
            return [];
        }

        var from = uploadedFromUtc ?? DateTime.MinValue;
        var attachments = await Session.Query<Attachment, AttachmentIndex>(
                index => index.OwnerKind.IsIn(kinds) && index.UploadedUtc < uploadedBeforeUtc && index.UploadedUtc >= from,
                collection: CollectionName)
            .OrderBy(index => index.UploadedUtc)
            .ThenBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken);

        return attachments.ToArray();
    }

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
