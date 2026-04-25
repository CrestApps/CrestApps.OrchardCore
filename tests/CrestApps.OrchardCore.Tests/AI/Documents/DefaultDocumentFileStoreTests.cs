using System.Text;
using CrestApps.OrchardCore.AI.Documents.Services;
using Moq;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Tests.AI.Documents;

public sealed class DefaultDocumentFileStoreTests
{
    [Fact]
    public async Task SaveFileAsync_UsesNormalizedRelativePath()
    {
        var expectedPath = Path.Combine("documents", "chat", "file.txt");

        var fileStore = new Mock<IFileStore>();
        fileStore
            .Setup(store => store.CreateFileFromStreamAsync(expectedPath, It.IsAny<Stream>(), true))
            .ReturnsAsync(expectedPath);

        var documentFileStore = new DefaultDocumentFileStore(fileStore.Object);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("test document"));

        var storedPath = await documentFileStore.SaveFileAsync("documents/chat/file.txt", content);

        Assert.Equal(expectedPath, storedPath);
    }

    [Fact]
    public async Task GetFileAsync_ReturnsNullWhenFileDoesNotExist()
    {
        var expectedPath = Path.Combine("documents", "chat", "missing.txt");

        var fileStore = new Mock<IFileStore>();
        fileStore
            .Setup(store => store.GetFileInfoAsync(expectedPath))
            .ReturnsAsync((IFileStoreEntry)null);

        var documentFileStore = new DefaultDocumentFileStore(fileStore.Object);

        var stream = await documentFileStore.GetFileAsync("documents/chat/missing.txt");

        Assert.Null(stream);
        fileStore.Verify(store => store.GetFileStreamAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteFileAsync_RejectsPathTraversal()
    {
        var fileStore = new Mock<IFileStore>();
        var documentFileStore = new DefaultDocumentFileStore(fileStore.Object);

        await Assert.ThrowsAsync<ArgumentException>(() => documentFileStore.DeleteFileAsync("../secrets.txt"));
    }
}
