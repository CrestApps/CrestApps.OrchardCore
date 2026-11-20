using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Pdf;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

public sealed class BusinessDocumentPdfRendererTests
{
    // A PNG image of one transparent pixel.
    internal const string OnePixelPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    [Fact]
    public async Task RenderAsync_WritesTheNumberTheTotalsAndEveryRow()
    {
        // Arrange
        var document = BusinessDocumentSamples.CreateInvoice(rows: 3);

        // Act
        var content = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var words = pdf.GetPages().SelectMany(page => page.GetWords()).Select(word => word.Text).ToList();

        Assert.Equal("Invoice INV-1001", pdf.Information.Title);
        Assert.Contains("INV-1001", words);
        Assert.Contains("$97.20", words);
        Assert.Contains("$7.20", words);
        Assert.Contains("Subtotal", words);
        Assert.Equal(3, words.Count(word => word.StartsWith("Item", StringComparison.Ordinal)));
        Assert.Contains("Fabrikam", words);
        Assert.Contains("TAX-778", string.Join(' ', words), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_WhenTheTableSpansPages_RepeatsItsHeaderOnEachPage()
    {
        // Arrange
        var document = BusinessDocumentSamples.CreateInvoice(rows: 120);

        // Act
        var content = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        using var pdf = PdfDocument.Open(content);

        Assert.True(pdf.NumberOfPages > 1, "The table should span more than one page.");

        foreach (var page in pdf.GetPages())
        {
            var text = GetText(page);

            if (page.GetWords().Any(word => word.Text.StartsWith("Item", StringComparison.Ordinal)))
            {
                Assert.Contains("Description", text, StringComparison.Ordinal);
                Assert.Contains("Qty", text, StringComparison.Ordinal);
            }

            Assert.Contains($"Page {page.Number} of {pdf.NumberOfPages}", text, StringComparison.Ordinal);
        }

        var items = pdf.GetPages().SelectMany(page => page.GetWords()).Count(word => word.Text.StartsWith("Item", StringComparison.Ordinal));

        Assert.Equal(120, items);
    }

    [Fact]
    public async Task RenderAsync_NeverSplitsARowThatFitsOnAPage()
    {
        // Arrange
        var document = BusinessDocumentSamples.CreateInvoice(rows: 0);

        for (var index = 1; index <= 60; index++)
        {
            document.Rows.Add(new BusinessDocumentRow
            {
                Cells = { ["Description"] = $"Row{index}", ["Quantity"] = "1", ["Amount"] = "$1.00" },
                Detail = $"First{index}\nSecond{index}\nLast{index}",
            });
        }

        // Act
        var content = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        using var pdf = PdfDocument.Open(content);
        var pages = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var page in pdf.GetPages())
        {
            foreach (var word in page.GetWords())
            {
                pages.TryAdd(word.Text, page.Number);
            }
        }

        Assert.True(pdf.NumberOfPages > 1, "The rows should span more than one page.");

        for (var index = 1; index <= 60; index++)
        {
            Assert.Equal(pages[$"Row{index}"], pages[$"Last{index}"]);
        }
    }

    [Fact]
    public async Task RenderAsync_WritesTheWatermarkOnEveryPage()
    {
        // Arrange
        var document = BusinessDocumentSamples.CreateInvoice(rows: 120);
        document.Watermark = "DRAFT";

        // Act
        var content = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        using var pdf = PdfDocument.Open(content);

        Assert.True(pdf.NumberOfPages > 1);
        Assert.All(pdf.GetPages(), page => Assert.Contains(page.GetWords(), word => word.Text == "DRAFT"));
    }

    [Fact]
    public async Task RenderAsync_PrintsOnThePaperSizeOfTheDocument()
    {
        // Arrange
        var letter = BusinessDocumentSamples.CreateInvoice();
        var a4 = BusinessDocumentSamples.CreateInvoice();
        a4.PaperSize = BusinessDocumentPaperSize.A4;

        // Act
        var letterContent = await CreateRenderer().RenderAsync(letter, TestContext.Current.CancellationToken);
        var a4Content = await CreateRenderer().RenderAsync(a4, TestContext.Current.CancellationToken);

        // Assert
        using var letterPdf = PdfDocument.Open(letterContent);
        using var a4Pdf = PdfDocument.Open(a4Content);

        Assert.Equal(612, letterPdf.GetPage(1).Width, 0);
        Assert.Equal(595, a4Pdf.GetPage(1).Width, 0);
    }

    [Fact]
    public async Task RenderAsync_DrawsTheLogoTheLoaderReads()
    {
        // Arrange
        var document = BusinessDocumentSamples.CreateInvoice();
        document.LogoUrl = "/media/logo.png";

        Assert.True(PdfImage.TryRead(Convert.FromBase64String(OnePixelPng), out var logo));

        // Act
        var content = await CreateRenderer(new FixedLogoLoader(logo)).RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        using var pdf = PdfDocument.Open(content);

        Assert.Single(pdf.GetPage(1).GetImages());
    }

    [Fact]
    public async Task RenderAsync_WritesADocumentWithNothingButATitle()
    {
        // Arrange
        var document = new BusinessDocument { Title = "Statement" };

        // Act
        var content = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        using var pdf = PdfDocument.Open(content);

        Assert.Equal(1, pdf.NumberOfPages);
        Assert.Contains("Statement", GetText(pdf.GetPage(1)), StringComparison.Ordinal);
    }

    private static BusinessDocumentPdfRenderer CreateRenderer(IBusinessDocumentLogoLoader logoLoader = null)
        => new(
            logoLoader ?? new NullBusinessDocumentLogoLoader(),
            new PassThroughStringLocalizer<BusinessDocumentPdfRenderer>(),
            NullLogger<BusinessDocumentPdfRenderer>.Instance);

    private static string GetText(Page page)
        => string.Join(' ', page.GetWords().Select(word => word.Text));

    private sealed class FixedLogoLoader : IBusinessDocumentLogoLoader
    {
        private readonly PdfImage _logo;

        public FixedLogoLoader(PdfImage logo)
        {
            _logo = logo;
        }

        public Task<PdfImage> LoadAsync(string logoUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(_logo);
    }
}
