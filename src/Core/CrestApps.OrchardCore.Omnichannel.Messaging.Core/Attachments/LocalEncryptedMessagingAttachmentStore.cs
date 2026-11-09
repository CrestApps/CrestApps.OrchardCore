using CrestApps.OrchardCore.Core.FileStorage;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// The default <see cref="IMessagingAttachmentStore"/>: pictures are kept in a folder of the tenant's own
/// application data, outside the public media library, and every file on disk is the data-protected ciphertext. A
/// picture is small enough to protect in one piece, so no streaming container is needed.
/// </summary>
public sealed class LocalEncryptedMessagingAttachmentStore : IMessagingAttachmentStore
{
    /// <summary>
    /// The data protection purpose the pictures are encrypted under.
    /// </summary>
    public const string ProtectorPurpose = "CrestApps.Omnichannel.Messaging.Attachments";

    /// <summary>
    /// The name of the folder, inside the tenant's application data, the pictures are kept in.
    /// </summary>
    public const string FolderName = "MessagingAttachments";

    private readonly EncryptedFileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalEncryptedMessagingAttachmentStore"/> class.
    /// </summary>
    /// <param name="fileStore">The tenant-scoped file store the encrypted pictures are written to.</param>
    /// <param name="dataProtectionProvider">The data protection provider the pictures are encrypted with.</param>
    public LocalEncryptedMessagingAttachmentStore(IFileStore fileStore, IDataProtectionProvider dataProtectionProvider)
    {
        _store = new EncryptedFileStore(fileStore, dataProtectionProvider, ProtectorPurpose);
    }

    /// <inheritdoc/>
    public Task StoreAsync(string attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(attachmentId);

        return _store.WriteAsync(attachmentId, content);
    }

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(string attachmentId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(attachmentId) ? Task.FromResult<byte[]>(null) : _store.ReadAsync(attachmentId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
        => _store.DeleteAsync(attachmentId);

    // The key is hashed into the file name, so no key can name a path outside the folder and two keys never share a
    // file.
    internal static string ResolvePath(string attachmentId)
        => EncryptedFileStore.ResolvePath(attachmentId);
}
