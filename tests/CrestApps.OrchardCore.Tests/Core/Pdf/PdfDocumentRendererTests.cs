using System.Buffers.Binary;
using System.IO.Compression;
using CrestApps.OrchardCore.Pdf;
using CrestApps.OrchardCore.Pdf.Models;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Core.Pdf;

public sealed class PdfDocumentRendererTests
{
    private readonly PdfDocumentRenderer _renderer = new();

    [Fact]
    public void Render_ShouldWritePagesOfTheLayoutSizeWithTheirText()
    {
        // Arrange
        var layout = new PdfDocumentLayout { Title = "Certificate 42", Author = "Learning" };
        var page = new PdfPageLayout(PdfPageSize.Letter.Landscape());

        page.Elements.Add(new PdfTextElement { Text = "Certificate of Completion", X = 72, Top = 72, FontSize = 28, Bold = true });
        page.Elements.Add(new PdfTextElement { Text = "Awarded to Zoë Ångström", X = 72, Top = 140, FontSize = 16 });
        layout.Pages.Add(page);
        layout.Pages.Add(new PdfPageLayout(PdfPageSize.A4));

        // Act
        using var document = PdfDocument.Open(_renderer.Render(layout));

        // Assert
        Assert.Equal(2, document.NumberOfPages);
        Assert.Equal("Certificate 42", document.Information.Title);
        Assert.Equal("Learning", document.Information.Author);

        var first = document.GetPage(1);

        Assert.Equal(792, first.Width, tolerance: 0.5);
        Assert.Equal(612, first.Height, tolerance: 0.5);
        Assert.Contains("Certificate of Completion", first.Text);

        // Without an installed TrueType font the document is written in Helvetica, which writes letters without accents.
        Assert.Contains(PdfFontSet.Installed.Regular is null ? "Zoe Angstrom" : "Zoë Ångström", first.Text);
        Assert.Equal(595.28, document.GetPage(2).Width, tolerance: 0.5);
    }

    [Fact]
    public void Render_WhenTheLayoutHasNoPage_ShouldWriteOneBlankLetterPage()
    {
        // Act
        using var document = PdfDocument.Open(_renderer.Render(new PdfDocumentLayout()));

        // Assert
        Assert.Equal(1, document.NumberOfPages);
        Assert.Equal(612, document.GetPage(1).Width, tolerance: 0.5);
    }

    [Fact]
    public void Render_ShouldWrapTextToItsWidthAndCenterEachLine()
    {
        // Arrange
        var layout = new PdfDocumentLayout();
        var page = new PdfPageLayout();

        page.Elements.Add(new PdfTextElement
        {
            Text = "has completed every lesson and passed the final assessment of the course",
            X = 156,
            Top = 100,
            Width = 300,
            FontSize = 14,
            Alignment = PdfTextAlignment.Center,
        });
        layout.Pages.Add(page);

        // Act
        using var document = PdfDocument.Open(_renderer.Render(layout));
        var words = document.GetPage(1).GetWords().ToList();

        // Assert
        var lines = words.GroupBy(word => Math.Round(word.BoundingBox.Bottom)).ToList();

        Assert.True(lines.Count >= 2, $"Expected the text to wrap, but it took {lines.Count} line.");

        foreach (var line in lines)
        {
            var left = line.Min(word => word.BoundingBox.Left);
            var right = line.Max(word => word.BoundingBox.Right);

            Assert.True(left >= 155 && right <= 457, $"A line runs from {left} to {right}, outside the box.");
            Assert.Equal(306, (left + right) / 2, tolerance: 3);
        }
    }

    [Fact]
    public void Render_WhenTextHasMoreLinesThanAllowed_ShouldEndTheLastLineWithAnEllipsis()
    {
        // Arrange
        var layout = new PdfDocumentLayout();
        var page = new PdfPageLayout();

        page.Elements.Add(new PdfTextElement
        {
            Text = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen",
            X = 72,
            Top = 72,
            Width = 120,
            MaxLines = 2,
        });
        layout.Pages.Add(page);

        // Act
        using var document = PdfDocument.Open(_renderer.Render(layout));
        var text = document.GetPage(1).Text;

        // Assert
        Assert.DoesNotContain("fifteen", text);
        Assert.True(text.EndsWith('…') || text.EndsWith("...", StringComparison.Ordinal), $"The text '{text}' does not end with an ellipsis.");
    }

    [Fact]
    public void Render_ShouldPlaceRightAlignedTextWithoutWidthSoItEndsAtX()
    {
        // Arrange
        var layout = new PdfDocumentLayout();
        var page = new PdfPageLayout();

        page.Elements.Add(new PdfTextElement { Text = "No. CERT-0042", X = 540, Top = 72, Alignment = PdfTextAlignment.Right });
        layout.Pages.Add(page);

        // Act
        using var document = PdfDocument.Open(_renderer.Render(layout));
        var words = document.GetPage(1).GetWords().ToList();

        // Assert
        Assert.Equal(540, words.Max(word => word.BoundingBox.Right), tolerance: 1);
    }

    [Fact]
    public void Render_ShouldDrawJpegAndPngImagesAndSkipOtherFiles()
    {
        // Arrange
        var layout = new PdfDocumentLayout();
        var page = new PdfPageLayout { BackgroundImage = CreatePng(4, 3) };

        page.Elements.Add(new PdfImageElement { Data = CreateJpegHeader(2, 2), X = 72, Top = 72, Width = 100, Height = 100 });
        page.Elements.Add(new PdfImageElement { Data = "GIF89a not an image we draw"u8.ToArray(), X = 72, Top = 200, Width = 50, Height = 50 });
        layout.Pages.Add(page);

        // Act
        using var document = PdfDocument.Open(_renderer.Render(layout));
        var images = document.GetPage(1).GetImages().ToList();

        // Assert
        Assert.Equal(2, images.Count);

        var background = images.Single(image => image.WidthInSamples == 4);

        Assert.Equal(612, background.BoundingBox.Width, tolerance: 0.5);
        Assert.Equal(792, background.BoundingBox.Height, tolerance: 0.5);
    }

    [Fact]
    public void Render_ShouldDrawAQrCodeAsSquaresOnAWhiteQuietZone()
    {
        // Arrange
        var layout = new PdfDocumentLayout();
        var page = new PdfPageLayout { BackgroundColor = new PdfColor(240, 240, 255) };

        page.Elements.Add(new PdfQrCodeElement { Value = "https://example.com/verify/ABCD-2345", X = 400, Top = 500, Size = 116 });
        page.Elements.Add(new PdfRectangleElement { X = 36, Top = 36, Width = 540, Height = 720, Outline = new PdfColor(30, 64, 175), OutlineThickness = 3 });
        page.Elements.Add(new PdfLineElement { X = 200, Top = 400, X2 = 412, Top2 = 400, Thickness = 0.75 });
        layout.Pages.Add(page);

        // Act
        using var document = PdfDocument.Open(_renderer.Render(layout));
        var paths = document.GetPage(1).Paths;

        // Assert
        // The page background, the quiet zone, the outline, the line and one square per run of dark modules.
        Assert.True(paths.Count > 50, $"Expected the QR code to draw its modules, but the page has {paths.Count} paths.");
    }

    [Fact]
    public void GetModules_ShouldHoldTheFinderPatternsAndTheQuietZone()
    {
        // Act
        var modules = PdfQrCode.GetModules("https://example.com/verify/ABCD-2345");

        // Assert
        var size = modules.GetLength(0);

        Assert.Equal(size, modules.GetLength(1));
        Assert.True(size >= 21 + 8, $"A QR code has at least 21 modules a side plus the quiet zone, but this one has {size}.");

        // The quiet zone is four light modules wide; the finder pattern starts with seven dark modules after it.
        Assert.All(Enumerable.Range(0, size), index => Assert.False(modules[0, index]));
        Assert.All(Enumerable.Range(4, 7), index => Assert.True(modules[4, index]));
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var stream = new MemoryStream();

        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];

        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(stream, "IHDR", header);

        using var raw = new MemoryStream();

        for (var row = 0; row < height; row++)
        {
            raw.WriteByte(0);

            for (var column = 0; column < width; column++)
            {
                raw.Write([(byte)(column * 60), (byte)(row * 80), 200]);
            }
        }

        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw.ToArray());
        }

        WriteChunk(stream, "IDAT", compressed.ToArray());
        WriteChunk(stream, "IEND", []);

        return stream.ToArray();
    }

    private static void WriteChunk(MemoryStream stream, string type, byte[] data)
    {
        var length = new byte[4];
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(typeBytes);
        stream.Write(data);

        var crc = new byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32([.. typeBytes, .. data]));
        stream.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in bytes)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    // The start of a baseline JPEG: PdfPig reads the size and the color components from the frame header and embeds
    // the file as it is, for the reader to decode.
    private static byte[] CreateJpegHeader(int width, int height)
    {
        return
        [
            0xFF, 0xD8,
            0xFF, 0xC0, 0x00, 0x11, 0x08,
            (byte)(height >> 8), (byte)height,
            (byte)(width >> 8), (byte)width,
            0x03,
            0x01, 0x11, 0x00,
            0x02, 0x11, 0x01,
            0x03, 0x11, 0x01,
            0xFF, 0xD9,
        ];
    }
}
