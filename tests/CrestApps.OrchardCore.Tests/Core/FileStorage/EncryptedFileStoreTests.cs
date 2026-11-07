using CrestApps.OrchardCore.Core.FileStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.FileStorage.FileSystem;

namespace CrestApps.OrchardCore.Tests.Core.FileStorage;

/// <summary>
/// The encrypted keyed store under the messaging pictures and the call recordings keeps only ciphertext on disk, under
/// a hashed file name.
/// </summary>
public sealed class EncryptedFileStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "encrypted-file-store-" + Guid.NewGuid().ToString("N"));
    private readonly EncryptedFileStore _store;

    public EncryptedFileStoreTests()
    {
        Directory.CreateDirectory(_folder);
        _store = new EncryptedFileStore(new FileSystemStore(_folder, NullLogger<FileSystemStore>.Instance), new EphemeralDataProtectionProvider(), "Tests.Purpose");
    }

    [Fact]
    public async Task AWrittenFile_ReadsBackAndIsCiphertextOnDisk()
    {
        // Arrange
        var content = "a secret picture"u8.ToArray();

        // Act
        await _store.WriteAsync("../escape attempt", content);
        var read = await _store.ReadAsync("../escape attempt", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(content, read);
        var file = Assert.Single(Directory.GetFiles(_folder));
        Assert.Equal(EncryptedFileStore.ResolvePath("../escape attempt"), Path.GetFileName(file));
        Assert.NotEqual(content, await File.ReadAllBytesAsync(file, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AMissingKey_ReadsAsNothingAndCountsAsDeleted()
    {
        // Act
        var read = await _store.ReadAsync("missing", TestContext.Current.CancellationToken);
        var deleted = await _store.DeleteAsync("missing");

        // Assert
        Assert.Null(read);
        Assert.True(deleted);
    }

    [Fact]
    public async Task Purge_DeletesEveryFile()
    {
        // Arrange
        await _store.WriteAsync("one", new byte[] { 1 });
        await _store.WriteAsync("two", new byte[] { 2 });

        // Act
        var purged = await _store.TryPurgeAllAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(purged);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
