using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

public sealed class BusinessDocumentTemplateManagerTests
{
    private readonly InMemoryBusinessDocumentTemplateStore _store = new();

    public BusinessDocumentTemplateManagerTests()
    {
        _store.Entries.AddRange(
        [
            new BusinessDocumentTemplate { ItemId = "every", Name = "Every kind", IsDefault = true, AccentColor = "#111111", Footer = "Every footer" },
            new BusinessDocumentTemplate { ItemId = "invoice", Name = "Invoices", IsDefault = true, Kinds = ["Invoice"], AccentColor = "#2563EB", LogoUrl = "/media/invoice.png", PaperSize = BusinessDocumentPaperSize.A4, Footer = "Invoice footer" },
            new BusinessDocumentTemplate { ItemId = "wholesale", Name = "Wholesale", Kinds = ["Invoice", "Estimate"], AccentColor = "#16A34A" },
            new BusinessDocumentTemplate { ItemId = "estimate", Name = "Estimates", Kinds = ["Estimate"], AccentColor = "#DC2626" },
        ]);
    }

    [Fact]
    public async Task GetForKindAsync_ReturnsTheDefaultMadeForTheKind_BeforeTheDefaultForEveryKind()
    {
        // Act
        var invoice = await CreateManager().GetForKindAsync("Invoice", cancellationToken: TestContext.Current.CancellationToken);
        var receipt = await CreateManager().GetForKindAsync("Receipt", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invoice", invoice?.ItemId);
        Assert.Equal("every", receipt?.ItemId);
    }

    [Fact]
    public async Task GetForKindAsync_ReturnsTheNamedTemplate_WhenItIsForTheKind()
    {
        // Act
        var template = await CreateManager().GetForKindAsync("Invoice", "wholesale", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("wholesale", template?.ItemId);
    }

    [Fact]
    public async Task GetForKindAsync_ReturnsTheDefault_WhenTheNamedTemplateIsForAnotherKind()
    {
        // Act
        var template = await CreateManager().GetForKindAsync("Invoice", "estimate", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invoice", template?.ItemId);
    }

    [Fact]
    public async Task GetForKindAsync_ReturnsTheDefault_WhenTheNamedTemplateDoesNotExist()
    {
        // Act
        var template = await CreateManager().GetForKindAsync("Invoice", "missing", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invoice", template?.ItemId);
    }

    [Fact]
    public async Task GetForKindAsync_ReturnsNull_WhenNoTemplateIsForTheKind()
    {
        // Arrange
        _store.Entries.RemoveAll(template => template.ItemId == "every");

        // Act
        var template = await CreateManager().GetForKindAsync("Receipt", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(template);
    }

    [Fact]
    public async Task ApplyAsync_CopiesTheTemplateWhereTheDocumentHasNothing()
    {
        // Arrange
        var document = new BusinessDocument { Kind = "Invoice" };

        // Act
        await CreateManager().ApplyAsync(document, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/media/invoice.png", document.LogoUrl);
        Assert.Equal("#2563EB", document.AccentColor);
        Assert.Equal(BusinessDocumentPaperSize.A4, document.PaperSize);
        Assert.Equal("Invoice footer", document.Footer);
    }

    [Fact]
    public async Task ApplyAsync_KeepsWhatTheDocumentHasOfItsOwn()
    {
        // Arrange
        var document = new BusinessDocument
        {
            Kind = "Invoice",
            LogoUrl = "/media/own.png",
            AccentColor = "#000000",
            PaperSize = BusinessDocumentPaperSize.Letter,
            Footer = "Own footer",
        };

        // Act
        await CreateManager().ApplyAsync(document, "wholesale", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("/media/own.png", document.LogoUrl);
        Assert.Equal("#000000", document.AccentColor);
        Assert.Equal(BusinessDocumentPaperSize.Letter, document.PaperSize);
        Assert.Equal("Own footer", document.Footer);
    }

    [Fact]
    public async Task ApplyAsync_LeavesTheDocumentAsItIs_WhenNoTemplateIsForItsKind()
    {
        // Arrange
        _store.Entries.Clear();
        var document = new BusinessDocument { Kind = "Invoice" };

        // Act
        await CreateManager().ApplyAsync(document, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(document.AccentColor);
        Assert.Equal(BusinessDocumentPaperSize.Default, document.PaperSize);
    }

    private BusinessDocumentTemplateManager CreateManager()
        => _store.CreateManager();
}
