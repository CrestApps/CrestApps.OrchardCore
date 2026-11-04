using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Core.FileStorage;

/// <summary>
/// Keeps files encrypted at rest in any <see cref="IFileStore"/>, addressed by an opaque key.
/// </summary>
/// <remarks>
/// The key is hashed into the file name, so no key can name a path outside the store and two keys never share a file.
/// Small files are protected in one piece with <see cref="WriteAsync(string, ReadOnlyMemory{byte})"/>; a caller that
/// streams a large file through its own encrypting format writes and reads the ciphertext with
/// <see cref="WriteCiphertextAsync"/> and <see cref="OpenCiphertextAsync"/>, using <see cref="Protector"/>.
/// </remarks>
public sealed class EncryptedFileStore
{
    /// <summary>
    /// The extension of every file the store writes.
    /// </summary>
    public const string ProtectedFileExtension = ".protected";

    private readonly IFileStore _fileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncryptedFileStore"/> class.
    /// </summary>
    /// <param name="fileStore">The file store the ciphertext is kept in.</param>
    /// <param name="dataProtectionProvider">The data protection provider the files are encrypted with.</param>
    /// <param name="purpose">The data protection purpose the files are encrypted under.</param>
    public EncryptedFileStore(IFileStore fileStore, IDataProtectionProvider dataProtectionProvider, string purpose)
    {
        ArgumentNullException.ThrowIfNull(fileStore);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        _fileStore = fileStore;
        Protector = dataProtectionProvider.CreateProtector(purpose);
    }

    /// <summary>
    /// Gets the protector the files are encrypted with.
    /// </summary>
    public IDataProtector Protector { get; }

    /// <summary>
    /// Gets the name of the file a key is kept in.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The file name.</returns>
    public static string ResolvePath(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ProtectedFileExtension;
    }

    /// <summary>
    /// Encrypts content in one piece and writes it under a key, replacing what the key held.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="content">The plaintext.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public async Task WriteAsync(string key, ReadOnlyMemory<byte> content)
    {
        using var stream = new MemoryStream(Protector.Protect(content.ToArray()), writable: false);

        await _fileStore.CreateFileFromStreamAsync(ResolvePath(key), stream, overwrite: true);
    }

    /// <summary>
    /// Reads and decrypts the content written under a key with <see cref="WriteAsync(string, ReadOnlyMemory{byte})"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>
    /// The plaintext, or <see langword="null"/> when the key holds nothing or was written under keys the tenant no longer
    /// holds.
    /// </returns>
    public async Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var stream = await OpenCiphertextAsync(key);

        if (stream is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();

        await stream.CopyToAsync(buffer, cancellationToken);

        try
        {
            return Protector.Unprotect(buffer.ToArray());
        }
        catch (CryptographicException)
        {
            // A file written under keys this tenant no longer holds is as good as gone.
            return null;
        }
    }

    /// <summary>
    /// Writes ciphertext the caller has already produced under a key, replacing what the key held.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="ciphertext">The encrypted content, read to its end.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public async Task WriteCiphertextAsync(string key, Stream ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        await _fileStore.CreateFileFromStreamAsync(ResolvePath(key), ciphertext, overwrite: true);
    }

    /// <summary>
    /// Opens the ciphertext kept under a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The ciphertext stream, which the caller disposes, or <see langword="null"/> when the key holds nothing.</returns>
    public async Task<Stream> OpenCiphertextAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        var path = ResolvePath(key);

        if (await _fileStore.GetFileInfoAsync(path) is null)
        {
            return null;
        }

        return await _fileStore.GetFileStreamAsync(path);
    }

    /// <summary>
    /// Deletes the file kept under a key. A key that holds nothing counts as deleted.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key holds nothing any more.</returns>
    public async Task<bool> DeleteAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var path = ResolvePath(key);

        if (await _fileStore.GetFileInfoAsync(path) is null)
        {
            return true;
        }

        return await _fileStore.TryDeleteFileAsync(path);
    }

    /// <summary>
    /// Deletes every file in the store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the purge.</param>
    /// <returns><see langword="true"/> when every file was deleted.</returns>
    public async Task<bool> TryPurgeAllAsync(CancellationToken cancellationToken = default)
    {
        var purged = true;

        await foreach (var entry in _fileStore.GetDirectoryContentAsync(includeSubDirectories: false).WithCancellation(cancellationToken))
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            if (!await _fileStore.TryDeleteFileAsync(entry.Path))
            {
                purged = false;
            }
        }

        return purged;
    }
}
