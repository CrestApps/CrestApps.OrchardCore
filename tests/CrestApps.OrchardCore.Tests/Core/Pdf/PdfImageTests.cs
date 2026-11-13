using CrestApps.OrchardCore.Pdf;

namespace CrestApps.OrchardCore.Tests.Core.Pdf;

public sealed class PdfImageTests
{
    [Fact]
    public void TryRead_ReadsTheSizeOfAPng()
    {
        // Arrange
        var data = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        // Act
        var read = PdfImage.TryRead(data, out var image);

        // Assert
        Assert.True(read);
        Assert.Equal(PdfImageFormat.Png, image.Format);
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
    }

    [Fact]
    public void TryRead_ReadsTheSizeOfAJpegFromItsFrameHeader()
    {
        // Arrange
        byte[] data =
        [
            0xFF, 0xD8,
            0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x20, 0x00, 0x40, 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
            0xFF, 0xD9,
        ];

        // Act
        var read = PdfImage.TryRead(data, out var image);

        // Assert
        Assert.True(read);
        Assert.Equal(PdfImageFormat.Jpeg, image.Format);
        Assert.Equal(64, image.Width);
        Assert.Equal(32, image.Height);
    }

    [Fact]
    public void TryRead_RefusesAFileThatIsNotAPngOrJpeg()
    {
        // Arrange
        var data = "GIF89a, not one of the formats a PDF image is read from"u8.ToArray();

        // Act
        var read = PdfImage.TryRead(data, out var image);

        // Assert
        Assert.False(read);
        Assert.Null(image);
    }
}
