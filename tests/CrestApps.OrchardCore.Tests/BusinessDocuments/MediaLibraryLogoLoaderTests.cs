using CrestApps.OrchardCore.BusinessDocuments.Services;
using Moq;
using OrchardCore.FileStorage;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

/// <summary>
/// A PDF's logo is read from the media library only: an address elsewhere is never fetched.
/// </summary>
public sealed class MediaLibraryLogoLoaderTests
{
    [Theory]
    [InlineData("/media/logo.png", "/media/", "logo.png")]
    [InlineData("/media/brand/logo%20dark.png?v=2", "/media/", "brand/logo dark.png")]
    [InlineData("https://shop.example.com/media/logo.png", "/media/", "logo.png")]
    [InlineData("//cdn.example.com/media/logo.png", "/media/", "logo.png")]
    [InlineData("/tenant/media/logo.jpg", "/tenant/media/", "logo.jpg")]
    [InlineData("https://cdn.example.com/media/logo.png", "https://cdn.example.com/media/", "logo.png")]
    [InlineData("brand/logo.png", "/media/", "brand/logo.png")]
    [InlineData("https://other.example.com/logo.png", "/media/", null)]
    [InlineData("/media/../App_Data/secret.png", "/media/", null)]
    [InlineData("/media/%2e%2e/App_Data/secret.png", "/media/", null)]
    [InlineData("file:///C:/secret.png", "/media/", null)]
    [InlineData("javascript:alert(1)", "/media/", null)]
    [InlineData("", "/media/", null)]
    public void GetMediaPath_AcceptsOnlyAnAddressInTheMediaLibrary(string url, string mediaBaseUrl, string expected)
    {
        // Act
        var path = MediaLibraryLogoLoader.GetMediaPath(url, mediaBaseUrl);

        // Assert
        Assert.Equal(expected, path);
    }

    [Fact]
    public async Task LoadAsync_ReadsAPngFromTheMediaLibrary()
    {
        // Arrange
        var store = CreateStore("logo.png", Convert.FromBase64String(BusinessDocumentPdfRendererTests.OnePixelPng));

        // Act
        var image = await new MediaLibraryLogoLoader(store.Object).LoadAsync("/media/logo.png", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(image);
        Assert.Equal(1, image.Width);
    }

    [Fact]
    public async Task LoadAsync_NeverOpensAFileThatIsNotAPngOrJpeg()
    {
        // Arrange
        var store = CreateStore("logo.svg", [1, 2, 3]);

        // Act
        var image = await new MediaLibraryLogoLoader(store.Object).LoadAsync("/media/logo.svg", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(image);
        store.Verify(value => value.GetFileStreamAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_RefusesAFileNamedPngThatIsNotAnImage()
    {
        // Arrange
        var store = CreateStore("logo.png", "<svg onload=alert(1)></svg>"u8.ToArray());

        // Act
        var image = await new MediaLibraryLogoLoader(store.Object).LoadAsync("/media/logo.png", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(image);
    }

    private static Mock<IMediaFileStore> CreateStore(string path, byte[] content)
    {
        var store = new Mock<IMediaFileStore>();
        var entry = new Mock<IFileStoreEntry>();

        entry.SetupGet(value => value.Length).Returns(content.Length);
        entry.SetupGet(value => value.IsDirectory).Returns(false);

        store.Setup(value => value.MapPathToPublicUrl(It.IsAny<string>())).Returns((string value) => "/media/" + value);
        store.Setup(value => value.GetFileInfoAsync(path)).ReturnsAsync(entry.Object);
        store.Setup(value => value.GetFileStreamAsync(path)).ReturnsAsync(() => new MemoryStream(content));

        return store;
    }
}
