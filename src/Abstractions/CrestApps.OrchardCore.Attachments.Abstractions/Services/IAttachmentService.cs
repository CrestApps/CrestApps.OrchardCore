using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Attachments.Services;

/// <summary>
/// Keeps the files attached to records. It applies the upload policy (size, count, and the format the file's own bytes
/// show), runs the site's file scanners, and keeps the bytes encrypted in the tenant's file store. It does not check
/// who may attach or read; callers that act for a user ask <see cref="IAttachmentAccessService"/> first.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Attaches files to a record. Every file is checked before any is stored, so a refusal never leaves the ones
    /// before it behind.
    /// </summary>
    /// <param name="uploads">The files.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<AttachmentResult> AddAsync(IReadOnlyList<AttachmentUpload> uploads, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an attachment.
    /// </summary>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attachment, or <see langword="null"/>.</returns>
    Task<Attachment> FindAsync(string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets attachments by id, skipping the ones that don't exist.
    /// </summary>
    /// <param name="attachmentIds">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attachments, in the order of the ids.</returns>
    Task<IReadOnlyList<Attachment>> GetAsync(IEnumerable<string> attachmentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the attachments of a record, newest first.
    /// </summary>
    /// <param name="owner">The record.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attachments.</returns>
    Task<IReadOnlyList<Attachment>> ListAsync(RecordRef owner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists, oldest first, a page of the attachments of records of some kinds that were uploaded before a time, such
    /// as for a sweep of files whose record never kept them.
    /// </summary>
    /// <param name="ownerKinds">The kinds of the records the attachments belong to.</param>
    /// <param name="uploadedBeforeUtc">The time they were uploaded before.</param>
    /// <param name="uploadedFromUtc">The time of the last attachment of the previous page (attachments uploaded at that
    /// time come again, so a caller skips those it has seen), or <see langword="null"/> for the first page.</param>
    /// <param name="take">How many at most.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The attachments.</returns>
    Task<IReadOnlyList<Attachment>> ListUploadedBeforeAsync(IReadOnlyCollection<string> ownerKinds, DateTime uploadedBeforeUtc, DateTime? uploadedFromUtc, int take, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attachment>>([]);

    /// <summary>
    /// Reads an attachment's bytes.
    /// </summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The bytes, or <see langword="null"/> when the file is gone.</returns>
    Task<byte[]> ReadAsync(Attachment attachment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an attachment and its file.
    /// </summary>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it existed.</returns>
    Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a link that downloads an attachment without signing in, for a customer who follows it from an e-mail.
    /// </summary>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="lifetime">How long the link works.</param>
    /// <returns>
    /// The path of the link from the host, with the tenant's path base, such as <c>/tenant-a/attachments/{id}?token=...</c>;
    /// put the site's scheme and host in front of it for an e-mail.
    /// </returns>
    string CreateSignedPath(string attachmentId, TimeSpan lifetime);
}
