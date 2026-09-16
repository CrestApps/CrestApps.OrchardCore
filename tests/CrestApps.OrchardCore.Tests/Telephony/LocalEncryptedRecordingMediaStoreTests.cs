using System.Security.Cryptography;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.FileStorage.FileSystem;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class LocalEncryptedRecordingMediaStoreTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalEncryptedRecordingMediaStore _store;

    public LocalEncryptedRecordingMediaStoreTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "crestapps-recording-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        var fileStore = new FileSystemStore(_rootPath, NullLogger<FileSystemStore>.Instance);
        _store = new LocalEncryptedRecordingMediaStore(fileStore, new EphemeralDataProtectionProvider());
    }

    [Fact]
    public async Task StoreAsync_ThenOpenReadAsync_ReturnsOriginalBytes()
    {
        // Arrange
        var content = new byte[] { 10, 20, 30, 40, 50 };
        var request = CreateRequest("crestapps-recording-interaction-1", content);

        // Act
        var reference = await _store.StoreAsync(request, TestContext.Current.CancellationToken);

        byte[] readBack;

        await using (var stream = await _store.OpenReadAsync(reference, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(stream);

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
            readBack = buffer.ToArray();
        }

        // Assert
        Assert.Equal(request.StorageKey, reference);
        Assert.Equal(content, readBack);
    }

    [Fact]
    public async Task StoreAsync_PersistsCiphertextThatDiffersFromPlaintext()
    {
        // Arrange
        var content = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var request = CreateRequest("crestapps-recording-interaction-2", content);

        // Act
        await _store.StoreAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var files = Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories);
        var storedFile = Assert.Single(files);
        var onDisk = await File.ReadAllBytesAsync(storedFile, TestContext.Current.CancellationToken);

        Assert.NotEqual(content, onDisk);
        Assert.False(ContainsSequence(onDisk, content));
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredRecording()
    {
        // Arrange
        var request = CreateRequest("crestapps-recording-interaction-3", [42, 42, 42]);
        var reference = await _store.StoreAsync(request, TestContext.Current.CancellationToken);

        // Act
        var deleted = await _store.DeleteAsync(reference, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleted);

        var stream = await _store.OpenReadAsync(reference, TestContext.Current.CancellationToken);

        Assert.Null(stream);
    }

    [Fact]
    public async Task DeleteAsync_WhenRecordingMissing_ConfirmsNoMediaRemains()
    {
        // Act
        var deleted = await _store.DeleteAsync("crestapps-recording-missing", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleted);
    }

    [Fact]
    public async Task OpenReadAsync_WhenReferenceMissing_ReturnsNull()
    {
        // Act
        var stream = await _store.OpenReadAsync("crestapps-recording-unknown", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(stream);
    }

    [Fact]
    public async Task TryPurgeAllAsync_RemovesEveryStoredRecording()
    {
        // Arrange
        var firstReference = await _store.StoreAsync(
            CreateRequest("crestapps-recording-interaction-4", [1, 2, 3]),
            TestContext.Current.CancellationToken);
        var secondReference = await _store.StoreAsync(
            CreateRequest("crestapps-recording-interaction-5", [4, 5, 6]),
            TestContext.Current.CancellationToken);

        // Act
        var purged = await _store.TryPurgeAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(purged);
        Assert.Null(await _store.OpenReadAsync(firstReference, TestContext.Current.CancellationToken));
        Assert.Null(await _store.OpenReadAsync(secondReference, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task StoreAsync_ThenOpenReadAsync_RoundTripsRecordingsSpanningManyChunks()
    {
        // Arrange
        // A recording larger than several 64 KiB frames exercises the multi-frame streaming path in both
        // directions, proving the chunked container reassembles byte-for-byte across frame boundaries.
        var content = new byte[(64 * 1024 * 3) + 517];
        new Random(12345).NextBytes(content);
        var request = CreateRequest("crestapps-recording-large", content);

        // Act
        var reference = await _store.StoreAsync(request, TestContext.Current.CancellationToken);

        byte[] readBack;

        await using (var stream = await _store.OpenReadAsync(reference, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(stream);

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
            readBack = buffer.ToArray();
        }

        // Assert
        Assert.Equal(content, readBack);
    }

    [Fact]
    public async Task StoreAsync_ThenOpenReadAsync_RoundTripsEmptyRecording()
    {
        // Arrange
        var request = CreateRequest("crestapps-recording-empty", []);

        // Act
        var reference = await _store.StoreAsync(request, TestContext.Current.CancellationToken);

        byte[] readBack;

        await using (var stream = await _store.OpenReadAsync(reference, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(stream);

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
            readBack = buffer.ToArray();
        }

        // Assert
        Assert.Empty(readBack);
    }

    [Fact]
    public async Task OpenReadAsync_WhenCiphertextTampered_ThrowsOnRead()
    {
        // Arrange
        var content = new byte[4096];
        new Random(777).NextBytes(content);
        var reference = await _store.StoreAsync(
            CreateRequest("crestapps-recording-tampered", content),
            TestContext.Current.CancellationToken);

        var storedFile = Assert.Single(Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories));
        var onDisk = await File.ReadAllBytesAsync(storedFile, TestContext.Current.CancellationToken);

        // Flip a byte deep in the ciphertext body so the authentication tag no longer matches.
        onDisk[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(storedFile, onDisk, TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
        {
            await using var stream = await _store.OpenReadAsync(reference, TestContext.Current.CancellationToken);

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task OpenReadAsync_WhenCiphertextTruncated_ThrowsOnRead()
    {
        // Arrange
        // Two full frames plus a partial one guarantees at least one non-final frame precedes the truncation,
        // so dropping the tail removes the final-frame marker and the reader must reject the clean EOF.
        var content = new byte[(64 * 1024 * 2) + 128];
        new Random(999).NextBytes(content);
        var reference = await _store.StoreAsync(
            CreateRequest("crestapps-recording-truncated", content),
            TestContext.Current.CancellationToken);

        var storedFile = Assert.Single(Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories));
        var onDisk = await File.ReadAllBytesAsync(storedFile, TestContext.Current.CancellationToken);

        // Drop the final frame so the container ends without its authenticated end-of-stream marker.
        var truncated = onDisk.AsSpan(0, onDisk.Length - (64 * 1024)).ToArray();
        await File.WriteAllBytesAsync(storedFile, truncated, TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
        {
            await using var stream = await _store.OpenReadAsync(reference, TestContext.Current.CancellationToken);

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task OpenReadAsync_WhenDataAppendedAfterFinalFrame_ThrowsOnRead()
    {
        // Arrange
        var content = new byte[2048];
        new Random(555).NextBytes(content);
        var reference = await _store.StoreAsync(
            CreateRequest("crestapps-recording-appended", content),
            TestContext.Current.CancellationToken);

        var storedFile = Assert.Single(Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories));
        var onDisk = await File.ReadAllBytesAsync(storedFile, TestContext.Current.CancellationToken);

        // Append stray bytes after the authenticated final frame; a faithful reader must reject them rather than
        // return a clean end-of-stream.
        var tampered = new byte[onDisk.Length + 4];
        onDisk.CopyTo(tampered, 0);
        await File.WriteAllBytesAsync(storedFile, tampered, TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
        {
            await using var stream = await _store.OpenReadAsync(reference, TestContext.Current.CancellationToken);

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private static RecordingMediaWriteRequest CreateRequest(string storageKey, byte[] content)
    {
        return new RecordingMediaWriteRequest
        {
            StorageKey = storageKey,
            InteractionId = storageKey,
            Format = "wav",
            Content = new MemoryStream(content, writable: false),
        };
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (var offset = 0; offset + needle.Length <= haystack.Length; offset++)
        {
            var matched = true;

            for (var index = 0; index < needle.Length; index++)
            {
                if (haystack[offset + index] != needle[index])
                {
                    matched = false;

                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }
}
