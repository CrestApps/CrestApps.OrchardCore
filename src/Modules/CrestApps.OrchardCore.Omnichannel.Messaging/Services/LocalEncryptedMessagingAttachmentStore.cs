using System.Security.Cryptography;
using System.Text;
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

    private const string ProtectedFileExtension = ".protected";

    private readonly IFileStore _fileStore;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalEncryptedMessagingAttachmentStore"/> class.
    /// </summary>
    /// <param name="fileStore">The tenant-scoped file store the encrypted pictures are written to.</param>
    /// <param name="dataProtectionProvider">The data protection provider the pictures are encrypted with.</param>
    public LocalEncryptedMessagingAttachmentStore(IFileStore fileStore, IDataProtectionProvider dataProtectionProvider)
    {
        _fileStore = fileStore;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    /// <inheritdoc/>
    public async Task StoreAsync(string attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(attachmentId);

        var protectedBytes = _protector.Protect(content.ToArray());

        using var stream = new MemoryStream(protectedBytes, writable: false);

        await _fileStore.CreateFileFromStreamAsync(ResolvePath(attachmentId), stream, overwrite: true);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attachmentId))
        {
            return null;
        }

        var path = ResolvePath(attachmentId);

        if (await _fileStore.GetFileInfoAsync(path) is null)
        {
            return null;
        }

        await using var stream = await _fileStore.GetFileStreamAsync(path);
        using var buffer = new MemoryStream();

        await stream.CopyToAsync(buffer, cancellationToken);

        try
        {
            return _protector.Unprotect(buffer.ToArray());
        }
        catch (CryptographicException)
        {
            // A file written under keys this tenant no longer holds is as good as gone.
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attachmentId))
        {
            return false;
        }

        var path = ResolvePath(attachmentId);

        if (await _fileStore.GetFileInfoAsync(path) is null)
        {
            return true;
        }

        return await _fileStore.TryDeleteFileAsync(path);
    }

    // The key is hashed into the file name, so no key can name a path outside the folder and two keys never share a
    // file.
    internal static string ResolvePath(string attachmentId)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(attachmentId))) + ProtectedFileExtension;
}
