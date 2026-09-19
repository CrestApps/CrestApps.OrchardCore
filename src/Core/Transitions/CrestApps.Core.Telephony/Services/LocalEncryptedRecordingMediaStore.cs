using System.Security.Cryptography;
using System.Text;
using CrestApps.Core.Telephony.Models;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.Core.Telephony.Services;

/// <summary>
/// Default <see cref="IRecordingMediaStore"/> implementation that persists conversation recordings through an
/// <see cref="IRecordingMediaFileStore"/> backend, encrypting every recording at rest with the data protection
/// provider. Recordings are addressed by a deterministic storage key so a read or a right-to-erasure delete
/// never needs any state beyond the key the orchestration layer already holds. The bytes the backend sees are
/// always the protected ciphertext, and both writes and reads stream the recording through a chunked
/// authenticated-encryption container (<see cref="RecordingMediaCryptoFormat"/>) so a whole recording is never
/// buffered in memory.
/// </summary>
/// <remarks>
/// The encryption and the naming stay here rather than in the backend, so a deployment that moves its
/// recordings from a directory to cloud storage changes where the bytes live without changing how they are
/// encrypted or which name a given storage key resolves to.
/// </remarks>
public sealed class LocalEncryptedRecordingMediaStore : IRecordingMediaStore, ISupportsTenantMediaPurge
{
    private const string ProtectedFileExtension = ".protected";

    private readonly IRecordingMediaFileStore _fileStore;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalEncryptedRecordingMediaStore"/> class.
    /// </summary>
    /// <param name="fileStore">The backend the encrypted recordings are written to.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to encrypt recordings at rest.</param>
    public LocalEncryptedRecordingMediaStore(
        IRecordingMediaFileStore fileStore,
        IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        _fileStore = fileStore;
        _protector = dataProtectionProvider.CreateProtector(TelephonyConstants.RecordingMediaProtectorPurpose);
    }

    /// <inheritdoc/>
    public async Task<string> StoreAsync(RecordingMediaWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.StorageKey);
        ArgumentNullException.ThrowIfNull(request.Content);

        // The plaintext recording is encrypted a fixed chunk at a time as the backend reads the wrapping
        // stream, so a large recording is never materialized in memory as a whole plaintext or ciphertext copy.
        await using var encryptingStream = RecordingMediaCryptoFormat.CreateEncryptingReadStream(request.Content, _protector, cancellationToken);

        await _fileStore.WriteAsync(ResolveName(request.StorageKey), encryptingStream, cancellationToken);

        return request.StorageKey;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(storageReference))
        {
            return null;
        }

        var storedStream = await _fileStore.OpenReadAsync(ResolveName(storageReference), cancellationToken);

        if (storedStream is null)
        {
            return null;
        }

        // The returned stream decrypts a fixed chunk at a time and takes ownership of the underlying stored
        // stream, so reading back a recording never buffers the whole plaintext in memory.
        try
        {
            return await RecordingMediaCryptoFormat.OpenDecryptingReadStreamAsync(storedStream, _protector, cancellationToken);
        }
        catch
        {
            await storedStream.DisposeAsync();

            throw;
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(storageReference))
        {
            return Task.FromResult(false);
        }

        return _fileStore.DeleteAsync(ResolveName(storageReference), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> TryPurgeAllAsync(CancellationToken cancellationToken = default)
    {
        var purged = true;

        await foreach (var name in _fileStore.ListAsync(cancellationToken))
        {
            if (!await _fileStore.DeleteAsync(name, cancellationToken))
            {
                purged = false;
            }
        }

        return purged;
    }

    private static string ResolveName(string storageKey)
    {
        // Derive a deterministic, collision-resistant, filesystem-safe file name from the opaque storage key.
        // Hashing the full key (rather than sanitizing characters) guarantees two distinct keys can never map
        // to the same file, so a read or delete addressed by the key can never return or remove another
        // recording's media.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(storageKey));

        return Convert.ToHexStringLower(hash) + ProtectedFileExtension;
    }
}
