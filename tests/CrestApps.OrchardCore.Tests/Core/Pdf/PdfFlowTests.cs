using CrestApps.OrchardCore.Pdf;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Core.Pdf;

public sealed class PdfFlowTests
{
    [Fact]
    public void WriteLines_AddsAPage_WhenTheNextLineDoesNotFit()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var regular = writer.AddStandardFont();
        var flow = new PdfFlow(writer, PdfPageSize.Letter, regular, writer.AddStandardFont(bold: true));
        var lineHeight = regular.LineHeight * 10;
        var linesPerPage = (int)Math.Floor(flow.PageContentHeight / lineHeight);
        var lines = Enumerable.Range(1, linesPerPage + 1).Select(number => "Line " + number).ToList();

        // Act
        flow.WriteLines(lines, regular, 10, PdfColor.Black);

        // Assert
        Assert.Equal(2, flow.PageCount);
        Assert.Equal(PdfFlow.Margin + lineHeight, flow.Top, 3);

        using var document = PdfDocument.Open(writer.Build());

        Assert.Equal(2, document.NumberOfPages);
        Assert.Contains("Line " + (linesPerPage + 1), document.GetPage(2).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureSpace_KeepsABlockTallerThanAPage_OnTheEmptyPage()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var flow = new PdfFlow(writer, PdfPageSize.A4, writer.AddStandardFont(), writer.AddStandardFont(bold: true));

        // Act
        flow.EnsureSpace(flow.PageContentHeight * 2);

        // Assert
        Assert.Equal(1, flow.PageCount);
        Assert.Equal(PdfFlow.Margin, flow.Top);
    }

    [Fact]
    public void AddSpace_DoesNotStartAPage_WithASpace()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var flow = new PdfFlow(writer, PdfPageSize.Letter, writer.AddStandardFont(), writer.AddStandardFont(bold: true));

        // Act
        flow.AddSpace(20);

        // Assert
        Assert.Equal(PdfFlow.Margin, flow.Top);
    }

    [Fact]
    public void PageStarted_DrawsOnEveryPage_BeforeItsContent()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var started = new List<PdfCanvas>();
        var flow = new PdfFlow(writer, PdfPageSize.Letter, writer.AddStandardFont(), writer.AddStandardFont(bold: true), started.Add);

        // Act
        flow.NewPage();

        // Assert
        Assert.Equal(2, started.Count);
        Assert.Same(flow.Page, started[1]);
        Assert.Equal(2, writer.PageCount);
    }

    [Fact]
    public void WriteFooters_NumbersEveryPage_AtTheRightOfTheFooter()
    {
        // Arrange
        using var writer = new PdfDocumentWriter();
        var regular = writer.AddStandardFont();
        var flow = new PdfFlow(writer, PdfPageSize.Letter, regular, writer.AddStandardFont(bold: true));
        flow.NewPage();

        // Act: a translation may put the numbers in another order, and hold other braces.
        flow.WriteFooters("{1} pages {x}: {0}", 8, PdfColor.Black);

        // Assert
        using var document = PdfDocument.Open(writer.Build());

        Assert.Equal("2 pages {x}: 1", document.GetPage(1).Text);
        Assert.Equal("2 pages {x}: 2", document.GetPage(2).Text);

        var letters = document.GetPage(2).Letters;

        Assert.Equal(PdfPageSize.Letter.Width - PdfFlow.Margin, letters[letters.Count - 1].EndBaseLine.X, 1);
    }

    [Theory]
    [InlineData("#2563EB", 0x25, 0x63, 0xEB)]
    [InlineData("2563eb", 0x25, 0x63, 0xEB)]
    [InlineData("  #FFFFFF ", 0xFF, 0xFF, 0xFF)]
    public void TryParseHex_ReadsSixHexadecimalDigits(string value, byte red, byte green, byte blue)
    {
        Assert.True(PdfColor.TryParseHex(value, out var color));
        Assert.Equal(new PdfColor(red, green, blue), color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("rgb(0,0,0)")]
    public void TryParseHex_RefusesWhatIsNotAColor(string value)
    {
        Assert.False(PdfColor.TryParseHex(value, out _));
    }

    [Fact]
    public void FromRgb_UnpacksTheBytes_AndIgnoresHigherBits()
    {
        Assert.Equal(new PdfColor(0x11, 0x18, 0x27), PdfColor.FromRgb(0xFF111827));
    }
}
