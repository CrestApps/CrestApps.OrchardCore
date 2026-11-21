using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// A package kept in a store that can't seek (Azure Blob Storage) is copied locally once, so a launch reads its files
/// without downloading the whole archive for every request; a store that seeks is read directly.
/// </summary>
public sealed class PackageArchiveCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "package-cache-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AStoreThatSeeks_IsReadDirectly_AndNothingIsCopied()
    {
        // Arrange
        var cache = new PackageArchiveCache(_folder, new LearningTestClock(), NullLogger.Instance);
        var source = new MemoryStream([1, 2, 3]);

        // Act
        await using var stream = await cache.OpenAsync("abc123", _ => Task.FromResult<Stream>(source), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(source, stream);
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public async Task AStoreThatCannotSeek_IsDownloadedOnce_ThenReadFromTheLocalCopy()
    {
        // Arrange
        var cache = new PackageArchiveCache(_folder, new LearningTestClock(), NullLogger.Instance);
        var downloads = 0;

        Task<Stream> Open(CancellationToken token)
        {
            Interlocked.Increment(ref downloads);

            return Task.FromResult<Stream>(new ForwardOnlyStream([1, 2, 3, 4]));
        }

        // Act
        var opened = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => cache.OpenAsync("abc123", Open, TestContext.Current.CancellationToken)));

        // Assert
        Assert.Equal(1, downloads);

        foreach (var stream in opened)
        {
            await using (stream)
            {
                Assert.True(stream.CanSeek);
                Assert.Equal(4, stream.Length);
            }
        }
    }

    [Fact]
    public async Task AMissingFile_ReturnsNull()
    {
        // Arrange
        var cache = new PackageArchiveCache(_folder, new LearningTestClock(), NullLogger.Instance);

        // Act
        var stream = await cache.OpenAsync("abc123", _ => Task.FromResult<Stream>(null), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(stream);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("")]
    public async Task ACraftedId_IsNeverUsedAsALocalPath(string fileId)
    {
        // Arrange
        var cache = new PackageArchiveCache(_folder, new LearningTestClock(), NullLogger.Instance);

        // Act
        await using var stream = await cache.OpenAsync(fileId, _ => Task.FromResult<Stream>(new ForwardOnlyStream([1])), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(stream.CanSeek);
        Assert.False(Directory.Exists(_folder));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class ForwardOnlyStream : MemoryStream
    {
        public ForwardOnlyStream(byte[] buffer)
            : base(buffer)
        {
        }

        public override bool CanSeek => false;
    }
}
