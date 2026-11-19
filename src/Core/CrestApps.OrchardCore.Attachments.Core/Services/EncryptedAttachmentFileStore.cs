using CrestApps.OrchardCore.Core.FileStorage;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Attachments.Core.Services;

/// <summary>
/// The <see cref="IAttachmentFileStore"/> on an <see cref="EncryptedFileStore"/>: every file is encrypted with the
/// tenant's data protection keys, and its name is a hash of the attachment id, so no id can name a path outside the
/// folder.
/// </summary>
public sealed class EncryptedAttachmentFileStore : IAttachmentFileStore
{
    /// <summary>
    /// The data protection purpose of the files.
    /// </summary>
    public const string ProtectorPurpose = "CrestApps.Attachments.Files";

    private readonly EncryptedFileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptedAttachmentFileStore"/> class.
    /// </summary>
    /// <param name="fileStore">The tenant file store.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public EncryptedAttachmentFileStore(IFileStore fileStore, IDataProtectionProvider dataProtectionProvider)
    {
        _store = new EncryptedFileStore(fileStore, dataProtectionProvider, ProtectorPurpose);
    }

    /// <inheritdoc/>
    public Task WriteAsync(string attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(attachmentId);

        return _store.WriteAsync(attachmentId, content);
    }

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(string attachmentId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(attachmentId) ? Task.FromResult<byte[]>(null) : _store.ReadAsync(attachmentId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(attachmentId) ? Task.FromResult(false) : _store.DeleteAsync(attachmentId);
}
