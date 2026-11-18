namespace CrestApps.OrchardCore.Attachments.Core.Services;

/// <summary>
/// Keeps the bytes of attachments, encrypted, in the tenant's application data or an Azure Blob container.
/// </summary>
public interface IAttachmentFileStore
{
    /// <summary>
    /// Writes a file.
    /// </summary>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the file is written.</returns>
    Task WriteAsync(string attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a file.
    /// </summary>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The bytes, or <see langword="null"/> when the file is gone.</returns>
    Task<byte[]> ReadAsync(string attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the file existed.</returns>
    Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default);
}
