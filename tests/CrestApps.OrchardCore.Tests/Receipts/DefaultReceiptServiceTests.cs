using CrestApps.OrchardCore.Receipts.Core;
using CrestApps.OrchardCore.Receipts.Core.Services;
using CrestApps.OrchardCore.Receipts.Models;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Receipts;

public sealed class DefaultReceiptServiceTests
{
    [Fact]
    public async Task BuildAsync_MergesConfiguredIssuerBranding()
    {
        // Arrange
        var settings = new ReceiptSettings
        {
            HeaderTitle = "Your receipt",
            BusinessName = "Acme Inc.",
            LogoUrl = "https://example.test/logo.png",
            BusinessAddress = "1 Main St",
            ContactEmail = "billing@example.test",
            ContactPhone = "+1 555 0100",
            Website = "https://example.test",
            FooterText = "Thank you",
            ShowTestPaymentBadge = true,
        };

        var service = CreateService(settings, siteName: "Fallback Site");
        var request = new ReceiptRequest();

        // Act
        var document = await service.BuildAsync(request);

        // Assert
        Assert.Equal("Your receipt", document.HeaderTitle);
        Assert.Equal("Acme Inc.", document.BusinessName);
        Assert.Equal("https://example.test/logo.png", document.LogoUrl);
        Assert.Equal("1 Main St", document.BusinessAddress);
        Assert.Equal("billing@example.test", document.ContactEmail);
        Assert.Equal("+1 555 0100", document.ContactPhone);
        Assert.Equal("https://example.test", document.Website);
        Assert.Equal("Thank you", document.FooterText);
        Assert.True(document.ShowTestBadge);
    }

    [Fact]
    public async Task BuildAsync_WhenBusinessNameIsEmpty_FallsBackToSiteName()
    {
        // Arrange
        var service = CreateService(new ReceiptSettings { BusinessName = "   " }, siteName: "Contoso");
        var request = new ReceiptRequest();

        // Act
        var document = await service.BuildAsync(request);

        // Assert
        Assert.Equal("Contoso", document.BusinessName);
    }

    [Fact]
    public async Task BuildAsync_ComputesSubtotalFromTotalAndTax()
    {
        // Arrange
        var service = CreateService(new ReceiptSettings());
        var request = new ReceiptRequest
        {
            Total = 108m,
            TaxAmount = 8m,
        };

        // Act
        var document = await service.BuildAsync(request);

        // Assert
        Assert.Equal(100m, document.Subtotal);
        Assert.Equal(108m, document.Total);
        Assert.Equal(8m, document.TaxAmount);
    }

    [Fact]
    public async Task BuildAsync_PassesThroughLineItemsAndTaxLines()
    {
        // Arrange
        var service = CreateService(new ReceiptSettings());
        var request = new ReceiptRequest
        {
            LineItems =
            [
                new ReceiptLineItem { Description = "Plan", Quantity = 1, Amount = 100m },
            ],
            TaxLines =
            [
                new ReceiptTaxLine { Description = "VAT", Amount = 8m },
            ],
        };

        // Act
        var document = await service.BuildAsync(request);

        // Assert
        var lineItem = Assert.Single(document.LineItems);
        Assert.Equal("Plan", lineItem.Description);

        var taxLine = Assert.Single(document.TaxLines);
        Assert.Equal("VAT", taxLine.Description);
        Assert.Equal(8m, taxLine.Amount);
    }

    [Fact]
    public async Task BuildAsync_WhenTestBadgeDisabled_DocumentDoesNotShowBadge()
    {
        // Arrange
        var service = CreateService(new ReceiptSettings { ShowTestPaymentBadge = false });
        var request = new ReceiptRequest { IsTest = true };

        // Act
        var document = await service.BuildAsync(request);

        // Assert
        Assert.False(document.ShowTestBadge);
        Assert.True(document.IsTest);
    }

    [Fact]
    public async Task BuildAsync_WhenRequestIsNull_Throws()
    {
        // Arrange
        var service = CreateService(new ReceiptSettings());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.BuildAsync(null).AsTask());
    }

    private static DefaultReceiptService CreateService(ReceiptSettings settings, string siteName = "Site")
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<ReceiptSettings>()).Returns(settings);
        site.SetupGet(s => s.SiteName).Returns(siteName);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new DefaultReceiptService(siteService.Object);
    }
}
