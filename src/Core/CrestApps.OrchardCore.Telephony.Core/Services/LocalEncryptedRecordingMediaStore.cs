using CrestApps.OrchardCore.Core.FileStorage;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="IRecordingMediaStore"/> implementation that persists conversation recordings to a
/// tenant-scoped file store, encrypting every recording at rest with the data protection provider. Recordings
/// are addressed by a deterministic storage key so a read or a right-to-erasure delete never needs any state
/// beyond the key the orchestration layer already holds. The bytes on disk are always the protected
/// ciphertext, and both writes and reads stream the recording through a chunked authenticated-encryption
/// container (<see cref="RecordingMediaCryptoFormat"/>) so a whole recording is never buffered in memory.
/// </summary>
public sealed class LocalEncryptedRecordingMediaStore : IRecordingMediaStore, ISupportsTenantMediaPurge
{
    private readonly EncryptedFileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalEncryptedRecordingMediaStore"/> class.
    /// </summary>
    /// <param name="fileStore">The tenant-scoped file store used to persist encrypted recordings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to encrypt recordings at rest.</param>
    public LocalEncryptedRecordingMediaStore(
        IFileStore fileStore,
        IDataProtectionProvider dataProtectionProvider)
    {
        _store = new EncryptedFileStore(fileStore, dataProtectionProvider, TelephonyConstants.RecordingMediaProtectorPurpose);
    }

    /// <inheritdoc/>
    public async Task<string> StoreAsync(RecordingMediaWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.StorageKey);
        ArgumentNullException.ThrowIfNull(request.Content);

        // The plaintext recording is encrypted a fixed chunk at a time as the file store reads the wrapping
        // stream, so a large recording is never materialized in memory as a whole plaintext or ciphertext copy.
        await using var encryptingStream = RecordingMediaCryptoFormat.CreateEncryptingReadStream(request.Content, _store.Protector, cancellationToken);
        await _store.WriteCiphertextAsync(request.StorageKey, encryptingStream);

        return request.StorageKey;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default)
    {
        // The returned stream decrypts a fixed chunk at a time and takes ownership of the underlying file
        // stream, so reading back a recording never buffers the whole plaintext in memory. Both the file system and
        // blob stores return seekable streams, so the decrypting stream seeks too and decrypts only the frames read.
        var fileStream = await _store.OpenCiphertextAsync(storageReference);

        if (fileStream is null)
        {
            return null;
        }

        try
        {
            return await RecordingMediaCryptoFormat.OpenDecryptingReadStreamAsync(fileStream, _store.Protector, cancellationToken);
        }
        catch
        {
            await fileStream.DisposeAsync();

            throw;
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
        => _store.DeleteAsync(storageReference);

    /// <inheritdoc/>
    public Task<bool> TryPurgeAllAsync(CancellationToken cancellationToken = default)
        => _store.TryPurgeAllAsync(cancellationToken);
}
