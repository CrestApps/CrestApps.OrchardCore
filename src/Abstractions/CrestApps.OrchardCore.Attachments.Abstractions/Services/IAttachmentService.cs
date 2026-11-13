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
    /// <returns>The site-relative path of the link.</returns>
    string CreateSignedPath(string attachmentId, TimeSpan lifetime);
}
