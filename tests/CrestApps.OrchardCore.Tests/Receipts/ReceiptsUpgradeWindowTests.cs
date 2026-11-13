using System.Text.Encodings.Web;
using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Receipts.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Receipts;

/// <summary>
/// Receipts became business documents. On a site upgraded before Business Documents is recorded as enabled, a receipt
/// must still be printed and e-mailed: Receipts registers the business document services it needs itself, and a
/// template store that cannot be read yet (its table not created) prints the receipt with its own look.
/// </summary>
public sealed class ReceiptsUpgradeWindowTests
{
    [Fact]
    public void ReceiptsStartup_RegistersTheBusinessDocumentServicesItNeeds()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new CrestApps.OrchardCore.Receipts.Startup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBusinessDocumentHtmlRenderer));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBusinessDocumentTemplateManager));
    }

    [Fact]
    public async Task RenderAsync_WhenTheTemplatesCannotBeReadYet_StillRendersTheReceipt()
    {
        // Arrange
        var templates = new Mock<IBusinessDocumentTemplateManager>();
        templates
            .Setup(manager => manager.ApplyAsync(It.IsAny<BusinessDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no such table: BusinessDocumentTemplateIndex"));

        var renderer = new DefaultReceiptHtmlRenderer(
            ReceiptRenderers.CreateMapper(),
            templates.Object,
            new BusinessDocumentHtmlRenderer(HtmlEncoder.Default, new PassThroughStringLocalizer<BusinessDocumentHtmlRenderer>()),
            NullLogger<DefaultReceiptHtmlRenderer>.Instance);

        // Act
        var html = await renderer.RenderAsync(
            new ReceiptDocument
            {
                BusinessName = "Acme Inc.",
                SourceLabel = "Receipt",
                Reference = "R-1001",
                Currency = "USD",
                Total = 10m,
                LineItems = [new ReceiptLineItem { Description = "Plan", Quantity = 1, Amount = 10m }],
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("R-1001", html, StringComparison.Ordinal);
        Assert.Contains("Acme Inc.", html, StringComparison.Ordinal);
    }
}
