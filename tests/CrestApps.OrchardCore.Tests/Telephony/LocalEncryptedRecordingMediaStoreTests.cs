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
    public async Task DeleteAsync_WhenRecordingMissing_ReturnsFalse()
    {
        // Act
        var deleted = await _store.DeleteAsync("crestapps-recording-missing", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task OpenReadAsync_WhenReferenceMissing_ReturnsNull()
    {
        // Act
        var stream = await _store.OpenReadAsync("crestapps-recording-unknown", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(stream);
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
            Content = content,
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
