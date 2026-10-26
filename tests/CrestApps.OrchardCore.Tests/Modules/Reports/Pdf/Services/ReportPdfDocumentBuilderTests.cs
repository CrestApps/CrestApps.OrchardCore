using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Pdf.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Pdf.Services;

public sealed class ReportPdfDocumentBuilderTests
{
    [Fact]
    public void Build_WhenNoFontIsInstalled_ShouldWriteInHelveticaAndReplaceWhatItCannotWrite()
    {
        // Arrange
        var document = new ReportDocument { Title = "Café Ünïcode 日本" }
            .Add(ReportSection.ForMetrics("Summary", [new ReportMetric("Calls", "8")]))
            .Add(ReportSection.ForTable("Queues", [new ReportColumn("Queue")], [new ReportRow(["Support"]), new ReportRow(["Total"], ReportRowKind.GrandTotal)]))
            .Add(ReportSection.ForChart("Mix", new ReportChart
            {
                Type = ReportChartType.Doughnut,
                Labels = ["Voice", "Email"],
                Datasets = [new ReportChartDataset("Calls", [3, 5])],
            }));

        // Act
        var content = new ReportPdfDocumentBuilder(document, "Page {0} of {1}", new ReportPdfFonts(null, null)).Build();

        // Assert
        using var pdf = PdfDocument.Open(content);
        var page = pdf.GetPage(1);
        var text = string.Join(' ', page.GetWords().Select(word => word.Text));

        Assert.All(page.Letters, letter => Assert.StartsWith("Helvetica", letter.FontName, StringComparison.Ordinal));
        Assert.Contains("Cafe Unicode ??", text);
        Assert.Contains("Support", text);
        Assert.Contains("Voice", text);
        Assert.Contains("Page 1 of 1", text);
    }

    [Fact]
    public void Build_WhenTheFontHasNoBoldFace_ShouldOutlineTheRegularFace()
    {
        // Arrange
        Assert.SkipWhen(ReportPdfFonts.Installed.Regular is null, "No TrueType font is installed.");

        var document = new ReportDocument { Title = "Agents" }
            .Add(ReportSection.ForMetrics("Summary", [new ReportMetric("Calls", "8", "Today")]));

        // Act
        var content = new ReportPdfDocumentBuilder(document, "Page {0} of {1}", new ReportPdfFonts(ReportPdfFonts.Installed.Regular, null)).Build();

        // Assert
        using var pdf = PdfDocument.Open(content);
        var words = pdf.GetPage(1).GetWords().ToList();

        Assert.All(Letters(words, "Agents"), letter => Assert.Equal(TextRenderingMode.FillThenStroke, letter.RenderingMode));
        Assert.All(Letters(words, "Today"), letter => Assert.Equal(TextRenderingMode.Fill, letter.RenderingMode));
    }

    [Fact]
    public void Build_WhenTheFontsAreInstalled_ShouldEmbedThem()
    {
        // Arrange
        Assert.SkipWhen(ReportPdfFonts.Installed.Regular is null, "No TrueType font is installed.");

        var document = new ReportDocument { Title = "Agents" }
            .Add(ReportSection.ForMetrics("Summary", [new ReportMetric("Calls", "8")]));

        // Act
        var content = new ReportPdfDocumentBuilder(document, "Page {0} of {1}", ReportPdfFonts.Installed).Build();

        // Assert
        using var pdf = PdfDocument.Open(content);

        Assert.DoesNotContain(pdf.GetPage(1).Letters, letter => letter.FontName.StartsWith("Helvetica", StringComparison.Ordinal));
    }

    private static IEnumerable<Letter> Letters(List<Word> words, string text)
    {
        var word = words.First(word => word.Text == text);

        return word.Letters;
    }
}
