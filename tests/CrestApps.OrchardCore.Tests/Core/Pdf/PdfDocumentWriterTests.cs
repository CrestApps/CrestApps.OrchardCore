using CrestApps.OrchardCore.Pdf;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Core.Pdf;

public sealed class PdfDocumentWriterTests
{
    private const string OnePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    [Fact]
    public void DrawText_PlacesTheTopOfTheLine_FromTheTopLeftOfThePage()
    {
        // Arrange
        using var writer = new PdfDocumentWriter("Placement");
        var font = writer.AddStandardFont();
        var canvas = writer.AddPage(PdfPageSize.Letter);

        // Act
        canvas.DrawText(font.Prepare("PAY"), font, 10, 72, 100, PdfColor.Black);

        // Assert
        using var document = PdfDocument.Open(writer.Build());
        var page = document.GetPage(1);
        var first = page.Letters[0];

        Assert.Equal("PAY", string.Concat(page.Letters.Select(letter => letter.Value)));
        Assert.Equal(72, first.StartBaseLine.X, 1);
        Assert.Equal(792 - 100 - (font.Ascent * 10), first.StartBaseLine.Y, 1);
    }

    [Fact]
    public void DrawTextOnBaseline_PlacesTheBaseline_WhereAsked()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var font = writer.AddStandardFont();
        var canvas = writer.AddPage(PdfPageSize.Letter);

        // Act: a baseline a quarter of an inch above the bottom edge.
        canvas.DrawTextOnBaseline(font.Prepare("123"), font, 12, 36, 792 - 18, PdfColor.Black);

        // Assert
        using var document = PdfDocument.Open(writer.Build());
        var first = document.GetPage(1).Letters[0];

        Assert.Equal(36, first.StartBaseLine.X, 1);
        Assert.Equal(18, first.StartBaseLine.Y, 1);
    }

    [Fact]
    public void DrawTextRight_EndsTheText_AtThePositionGiven()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var font = writer.AddStandardFont();
        var canvas = writer.AddPage(PdfPageSize.Letter);
        var text = font.Prepare("1,250.75");

        // Act
        canvas.DrawTextRight(text, font, 10, 540, 200, PdfColor.Black);

        // Assert
        using var document = PdfDocument.Open(writer.Build());
        var letters = document.GetPage(1).Letters;

        Assert.Equal(540 - font.Measure(text, 10), letters[0].StartBaseLine.X, 1);
        Assert.Equal(540, letters[letters.Count - 1].EndBaseLine.X, 1);
    }

    [Fact]
    public void AddPage_AddsEachPage_InItsOwnSize()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();

        // Act
        writer.AddPage(PdfPageSize.Letter);
        writer.AddPage(PdfPageSize.FromInches(8.5, 3.5));

        // Assert
        using var document = PdfDocument.Open(writer.Build());

        Assert.Equal(2, writer.PageCount);
        Assert.Equal(2, document.NumberOfPages);
        Assert.Equal(792, document.GetPage(1).Height, 1);
        Assert.Equal(252, document.GetPage(2).Height, 1);
        Assert.Equal(612, document.GetPage(2).Width, 1);
    }

    [Fact]
    public void AddPage_WithoutASize_Throws()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => writer.AddPage(new PdfPageSize(0, 792)));
    }

    [Fact]
    public void DrawImage_APngPicture_IsDrawnInItsRectangle()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var canvas = writer.AddPage(PdfPageSize.Letter);

        // Act
        var drawn = canvas.DrawImage(Convert.FromBase64String(OnePixelPng), 72, 144, 100, 40);

        // Assert
        using var document = PdfDocument.Open(writer.Build());
        var image = Assert.Single(document.GetPage(1).GetImages());

        Assert.True(drawn);
        Assert.Equal(72, image.BoundingBox.Left, 1);
        Assert.Equal(792 - 144 - 40, image.BoundingBox.Bottom, 1);
        Assert.Equal(100, image.BoundingBox.Width, 1);
        Assert.Equal(40, image.BoundingBox.Height, 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bm90IGEgcGljdHVyZQ==")]
    public void DrawImage_AFileThatIsNotAPicture_DrawsNothing(string file)
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var canvas = writer.AddPage(PdfPageSize.Letter);

        // Act
        var drawn = canvas.DrawImage(file is null ? null : Convert.FromBase64String(file), 0, 0, 10, 10);

        // Assert
        using var document = PdfDocument.Open(writer.Build());

        Assert.False(drawn);
        Assert.Empty(document.GetPage(1).GetImages());
    }

    [Fact]
    public void AddSymbolFont_AFontThatHasTheCharacters_WritesThem()
    {
        // Arrange
        var installed = PdfFontSet.GetInstalled("Arial").Regular;

        Assert.SkipWhen(installed is null, "No TrueType font is installed.");

        using var writer = new PdfDocumentWriter();

        // Act
        var font = writer.AddSymbolFont(installed, "0123456789");
        var canvas = writer.AddPage(PdfPageSize.Letter);

        canvas.DrawText("0042", font, 12, 10, 10, PdfColor.Black);

        // Assert
        using var document = PdfDocument.Open(writer.Build());

        Assert.NotNull(font);
        Assert.Equal("0042", string.Concat(document.GetPage(1).Letters.Select(letter => letter.Value)));
    }

    [Fact]
    public void AddSymbolFont_AFileThatIsNotAFont_ReturnsNothing()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();

        // Act & Assert
        Assert.Null(writer.AddSymbolFont([1, 2, 3], "0"));
        Assert.Null(writer.AddFont(null));
    }

    [Fact]
    public void PageSize_Landscape_TurnsThePageOnItsSide()
    {
        // Act
        var size = PdfPageSize.Letter.Landscape;

        // Assert
        Assert.Equal(792, size.Width);
        Assert.Equal(612, size.Height);
    }
}
