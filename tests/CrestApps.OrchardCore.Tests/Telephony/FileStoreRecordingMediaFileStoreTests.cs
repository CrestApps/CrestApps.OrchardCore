using CrestApps.Core.Telephony.Services;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.FileStorage.FileSystem;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The Azure recording module keeps the encrypted store and swaps only the backend underneath it, so the
/// adapter that binds an Orchard file store to that backend has to behave exactly like the framework's own.
/// Until this existed, the only backend any test exercised was the local one.
/// </summary>
public sealed class FileStoreRecordingMediaFileStoreTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalEncryptedRecordingMediaStore _store;

    public FileStoreRecordingMediaFileStoreTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "crestapps-recording-adapter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _store = new LocalEncryptedRecordingMediaStore(
            new FileStoreRecordingMediaFileStore(new FileSystemStore(_rootPath, NullLogger<FileSystemStore>.Instance)),
            new EphemeralDataProtectionProvider());
    }

    [Fact]
    public async Task StoreAsync_ThenOpenReadAsync_ReturnsOriginalBytes()
    {
        // Arrange
        var content = new byte[] { 9, 8, 7, 6 };

        // Act
        var reference = await _store.StoreAsync(CreateRequest("crestapps-adapter-1", content), TestContext.Current.CancellationToken);

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
    public async Task StoreAsync_AddressesTheRecordingByTheSameNameTheLocalBackendWould()
    {
        // Arrange
        // A deployment that moves from a directory to cloud storage keeps its recordings only if the name a
        // storage key resolves to is decided above the backend, not inside it.
        var localRootPath = Path.Combine(Path.GetTempPath(), "crestapps-recording-local-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(localRootPath);

        try
        {
            var localStore = new LocalEncryptedRecordingMediaStore(
                new LocalRecordingMediaFileStore(localRootPath),
                new EphemeralDataProtectionProvider());

            // Act
            await _store.StoreAsync(CreateRequest("crestapps-adapter-same-name", [1]), TestContext.Current.CancellationToken);
            await localStore.StoreAsync(CreateRequest("crestapps-adapter-same-name", [1]), TestContext.Current.CancellationToken);

            // Assert
            var throughAdapter = Path.GetFileName(Assert.Single(Directory.GetFiles(_rootPath)));
            var throughLocal = Path.GetFileName(Assert.Single(Directory.GetFiles(localRootPath)));

            Assert.Equal(throughLocal, throughAdapter);
        }
        finally
        {
            Directory.Delete(localRootPath, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteAsync_WhenRecordingMissing_ConfirmsNoMediaRemains()
    {
        // Act
        var deleted = await _store.DeleteAsync("crestapps-adapter-missing", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(deleted);
    }

    [Fact]
    public async Task TryPurgeAllAsync_RemovesEveryStoredRecording()
    {
        // Arrange
        var first = await _store.StoreAsync(CreateRequest("crestapps-adapter-2", [1, 2]), TestContext.Current.CancellationToken);
        var second = await _store.StoreAsync(CreateRequest("crestapps-adapter-3", [3, 4]), TestContext.Current.CancellationToken);

        // Act
        var purged = await _store.TryPurgeAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(purged);
        Assert.Null(await _store.OpenReadAsync(first, TestContext.Current.CancellationToken));
        Assert.Null(await _store.OpenReadAsync(second, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(_rootPath, "*", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private static RecordingMediaWriteRequest CreateRequest(string storageKey, byte[] content)
        => new()
        {
            StorageKey = storageKey,
            InteractionId = storageKey,
            Format = "wav",
            Content = new MemoryStream(content),
        };
}
