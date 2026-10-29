using CrestApps.OrchardCore.Reports.Pdf.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Pdf.Services;

public sealed class ReportPdfFontTests
{
    [Fact]
    public void Prepare_WhenTheFontHasNoGlyphForAnInvisibleCharacter_ShouldLeaveItOut()
    {
        // Arrange
        using var builder = new PdfDocumentBuilder();
        var font = ReportPdfFont.FromStandard(builder, Standard14Font.Helvetica);

        // Act
        // A byte order mark, then a heart with the variation selector that shows it as an emoji, and an emoji.
        var byteOrderMark = font.Prepare("\U0000FEFFQueue");
        var emoji = font.Prepare("\U00002764\U0000FE0F \U0001F600");

        // Assert
        Assert.Equal("Queue", byteOrderMark);
        Assert.Equal("? ?", emoji);
    }

    [Fact]
    public void Prepare_WhenAnAccentFollowsItsLetter_ShouldWriteTheLetterWithoutTheAccentTheFontLacks()
    {
        // Arrange
        using var builder = new PdfDocumentBuilder();
        var font = ReportPdfFont.FromStandard(builder, Standard14Font.Helvetica);

        // Act
        // An e and a combining acute accent, then half of a surrogate pair, which cannot be normalized.
        var text = font.Prepare("Cafe\U00000301 \uD800");

        // Assert
        Assert.Equal("Cafe ?", text);
    }

    [Fact]
    public void Prepare_WhenAnAccentFollowsItsLetter_ShouldWriteTheAccentedLetterOfTheFont()
    {
        // Arrange
        Assert.SkipWhen(ReportPdfFonts.Installed.Regular is null, "No TrueType font is installed.");

        using var builder = new PdfDocumentBuilder();
        var font = ReportPdfFonts.Installed.CreateRegular(builder);

        // Act
        var text = font.Prepare("Cafe\U00000301");

        // Assert
        Assert.Equal("Caf\U000000E9", text);
    }

    [Fact]
    public void Measure_WhenTheFontListsItsMacRomanCharactersFirst_ShouldGiveTheWidthsThePdfIsWrittenWith()
    {
        // Arrange
        // Georgia maps its characters for Mac Roman before Unicode, so its accented letters are other glyphs there.
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "georgia.ttf");

        Assert.SkipUnless(File.Exists(path), "Georgia is not installed.");

        using var builder = new PdfDocumentBuilder();
        var font = ReportPdfFont.FromTrueType(builder, File.ReadAllBytes(path), simulatesBold: false);
        var text = font.Prepare(new string(Enumerable.Range(0xC0, 64).Select(code => (char)code).ToArray()));

        // Act
        font.Write(builder.AddPage(PageSize.A4), text, 10, 20, 700);

        // Assert
        using var pdf = PdfDocument.Open(builder.Build());
        var letters = pdf.GetPage(1).Letters;

        Assert.Equal(64, letters.Count);
        Assert.All(letters, letter => Assert.Equal(font.Measure(letter.Value[0], 10), letter.Width, 0.001));
    }
}
