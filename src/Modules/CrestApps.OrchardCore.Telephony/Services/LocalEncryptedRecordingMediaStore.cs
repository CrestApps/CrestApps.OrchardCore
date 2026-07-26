using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="IRecordingMediaStore"/> implementation that persists conversation recordings to a
/// tenant-scoped file store, encrypting every recording at rest with the data protection provider. Recordings
/// are addressed by a deterministic storage key so a read or a right-to-erasure delete never needs any state
/// beyond the key the orchestration layer already holds. The bytes on disk are always the protected
/// ciphertext; the plaintext recording only ever exists in memory while it is being ingested or read back.
/// </summary>
public sealed class LocalEncryptedRecordingMediaStore : IRecordingMediaStore
{
    private const string ProtectedFileExtension = ".protected";

    private readonly IFileStore _fileStore;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalEncryptedRecordingMediaStore"/> class.
    /// </summary>
    /// <param name="fileStore">The tenant-scoped file store used to persist encrypted recordings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to encrypt recordings at rest.</param>
    public LocalEncryptedRecordingMediaStore(
        IFileStore fileStore,
        IDataProtectionProvider dataProtectionProvider)
    {
        _fileStore = fileStore;
        _protector = dataProtectionProvider.CreateProtector(TelephonyConstants.RecordingMediaProtectorPurpose);
    }

    /// <inheritdoc/>
    public async Task<string> StoreAsync(RecordingMediaWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.StorageKey);
        ArgumentNullException.ThrowIfNull(request.Content);

        var path = ResolvePath(request.StorageKey);
        var protectedBytes = _protector.Protect(request.Content);

        using var stream = new MemoryStream(protectedBytes, writable: false);
        await _fileStore.CreateFileFromStreamAsync(path, stream, overwrite: true);

        return request.StorageKey;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(storageReference))
        {
            return null;
        }

        var path = ResolvePath(storageReference);

        if (await _fileStore.GetFileInfoAsync(path) is null)
        {
            return null;
        }

        byte[] protectedBytes;

        await using (var stream = await _fileStore.GetFileStreamAsync(path))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            protectedBytes = buffer.ToArray();
        }

        var content = _protector.Unprotect(protectedBytes);

        return new MemoryStream(content, writable: false);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(storageReference))
        {
            return false;
        }

        var path = ResolvePath(storageReference);

        return await _fileStore.TryDeleteFileAsync(path);
    }

    private static string ResolvePath(string storageKey)
    {
        // Derive a deterministic, collision-resistant, filesystem-safe file name from the opaque storage key.
        // Hashing the full key (rather than sanitizing characters) guarantees two distinct keys can never map
        // to the same file, so a read or delete addressed by the key can never return or remove another
        // recording's media.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(storageKey));

        return Convert.ToHexStringLower(hash) + ProtectedFileExtension;
    }
}
