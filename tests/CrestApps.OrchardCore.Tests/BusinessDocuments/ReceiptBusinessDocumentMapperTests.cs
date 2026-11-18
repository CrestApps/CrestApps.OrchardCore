using System.Globalization;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

/// <summary>
/// A receipt is printed as a business document of the receipt kind, and its e-mail body comes from the shared HTML
/// renderer.
/// </summary>
public sealed class ReceiptBusinessDocumentMapperTests
{
    [Fact]
    public void Map_CarriesEveryPartOfTheReceipt()
    {
        // Arrange
        using var culture = CultureScope.Create(CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-US"));
        var receipt = CreateReceipt();

        // Act
        var document = ReceiptRenderers.CreateMapper().Map(receipt);

        // Assert
        Assert.Equal(ReceiptsConstants.BusinessDocumentKind, document.Kind);
        Assert.Equal("Your receipt", document.Title);
        Assert.Equal("Acme Inc.", document.Issuer.Name);
        Assert.Equal(new[] { "1 Main St", "Springfield" }, document.Issuer.Lines);
        Assert.Equal("billing@acme.example", document.Issuer.Email);
        Assert.Equal("+1 555 0100", document.Issuer.Phone);
        Assert.Equal("https://acme.example", document.Issuer.Website);
        Assert.Equal("Jane Doe", document.Recipient.Name);
        Assert.Equal("jane@example.com", document.Recipient.Email);
        Assert.Equal("Billed to", document.RecipientTitle);
        Assert.Equal("https://acme.example/logo.png", document.LogoUrl);
        Assert.Equal("Thank you", document.Footer);
        Assert.Equal(new BusinessDocumentStatus("Paid", BusinessDocumentTone.Success).Label, document.Status.Label);
        Assert.Equal(BusinessDocumentTone.Success, document.Status.Tone);
        Assert.Contains(document.Fields, field => field.Label == "Receipt" && field.Value == "R-1001");
        Assert.Contains(document.Fields, field => field.Label == "Date" && field.Value == receipt.IssuedAt.ToString("g", CultureInfo.CurrentCulture));
        Assert.Equal(2, document.Rows.Count);
        Assert.Equal("Plan", document.Rows[0].Cells["Description"]);
        Assert.Equal("2", document.Rows[0].Cells["Quantity"]);
        Assert.Equal("$90.00", document.Rows[0].Cells["Amount"]);
        Assert.Equal(new[] { "Subtotal", "State tax", "Total paid" }, document.Totals.Select(total => total.Label));
        Assert.True(document.Totals.Last().Emphasized);
        Assert.Equal("$108.00", document.Totals.Last().Value);
        Assert.True(document.Totals[1].Muted);
        Assert.Contains(document.Sections, documentSection => documentSection.Title == "Notes" && documentSection.Text == "Paid by card.");
        Assert.Null(document.Watermark);
    }

    /// <summary>
    /// Amounts print with a symbol, and the US and Canadian dollars both print "$". The receipt must still say which
    /// currency was paid.
    /// </summary>
    [Fact]
    public async Task MapAndRender_NameTheCurrency_WhenItsSymbolIsShared()
    {
        // Arrange
        using var culture = CultureScope.Create(CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-US"));
        var receipt = CreateReceipt();
        receipt.Currency = "CAD";

        // Act
        var document = ReceiptRenderers.CreateMapper().Map(receipt);
        var html = await ReceiptRenderers.CreateHtmlRenderer().RenderAsync(receipt, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(document.Fields, field => field.Label == "Currency" && field.Value == "CAD");
        Assert.Contains("CAD", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_MarksATestPaymentWithAWatermark_AndADueReceiptWithAWarning()
    {
        // Arrange
        var receipt = CreateReceipt();
        receipt.IsTest = true;
        receipt.ShowTestBadge = true;
        receipt.Status = ReceiptStatus.Due;

        // Act
        var document = ReceiptRenderers.CreateMapper().Map(receipt);

        // Assert
        Assert.Equal("Test payment", document.Watermark);
        Assert.Equal("Due", document.Status.Label);
        Assert.Equal(BusinessDocumentTone.Warning, document.Status.Tone);
        Assert.Equal("Total", document.Totals.Last().Label);
    }

    [Fact]
    public void Map_LeavesTheTaxLinesOut_WhenTheReceiptHasNoTax()
    {
        // Arrange
        var receipt = new ReceiptDocument
        {
            Currency = "USD",
            Total = 10m,
            Subtotal = 10m,
            LineItems = [new ReceiptLineItem { Description = "Plan", Quantity = 1, Amount = 10m }],
        };

        // Act
        var document = ReceiptRenderers.CreateMapper().Map(receipt);

        // Assert
        var total = Assert.Single(document.Totals);

        Assert.Equal("Payment receipt", document.Title);
        Assert.True(total.Emphasized);
        Assert.Empty(document.Sections);
    }

    [Fact]
    public async Task RenderAsync_WritesTheReceiptThroughTheSharedRenderer_AndEncodesItsValues()
    {
        // Arrange
        using var culture = CultureScope.Create(CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("en-US"));
        var receipt = CreateReceipt();
        receipt.BusinessName = "<script>alert(1)</script>";
        receipt.LineItems[0].Description = "<b>Plan</b>";

        // Act
        var html = await ReceiptRenderers.CreateHtmlRenderer().RenderAsync(receipt, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Plan</b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("R-1001", html, StringComparison.Ordinal);
        Assert.Contains("Total paid", html, StringComparison.Ordinal);
        Assert.Contains("$108.00", html, StringComparison.Ordinal);
        Assert.Contains("Billed to", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_UsesTheTemplateForReceipts_WhereTheReceiptHasNoLookOfItsOwn()
    {
        // Arrange
        var templates = new InMemoryBusinessDocumentTemplateStore();

        templates.Entries.Add(new BusinessDocumentTemplate { ItemId = "receipts", Name = "Receipts", Kinds = [ReceiptsConstants.BusinessDocumentKind], IsDefault = true, AccentColor = "#16A34A", Footer = "Template footer" });

        var receipt = CreateReceipt();

        // Act
        var html = await ReceiptRenderers.CreateHtmlRenderer(templates).RenderAsync(receipt, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("#16A34A", html, StringComparison.Ordinal);
        Assert.Contains("Thank you", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Template footer", html, StringComparison.Ordinal);
    }

    private static ReceiptDocument CreateReceipt()
        => new()
        {
            HeaderTitle = "Your receipt",
            BusinessName = "Acme Inc.",
            LogoUrl = "https://acme.example/logo.png",
            BusinessAddress = "1 Main St\nSpringfield",
            ContactEmail = "billing@acme.example",
            ContactPhone = "+1 555 0100",
            Website = "https://acme.example",
            FooterText = "Thank you",
            BilledToName = "Jane Doe",
            BilledToEmail = "jane@example.com",
            Reference = "R-1001",
            SourceLabel = "Receipt",
            IssuedAt = new DateTime(2026, 5, 1, 14, 30, 0),
            Currency = "USD",
            LineItems =
            [
                new ReceiptLineItem { Description = "Plan", Quantity = 2, UnitAmount = 45m, Amount = 90m },
                new ReceiptLineItem { Description = "Setup", Quantity = 1, UnitAmount = 10m, Amount = 10m },
            ],
            Subtotal = 100m,
            TaxLines = [new ReceiptTaxLine { Description = "State tax", Amount = 8m }],
            TaxAmount = 8m,
            Total = 108m,
            Status = ReceiptStatus.Paid,
            Notes = "Paid by card.",
        };
}
