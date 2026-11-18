using System.Text.Encodings.Web;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Receipts.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Environment.Shell;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Receipts;

/// <summary>
/// Receipts became business documents. On a site upgraded before Business Documents is recorded as enabled, a receipt
/// must still be printed and e-mailed: Receipts registers the business document services it needs itself, and a
/// receipt is printed with its own look, without querying the template store, until the feature is recorded as enabled.
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
    public async Task RenderAsync_BeforeBusinessDocumentsIsRecordedAsEnabled_IssuesNoTemplateQuery_AndStillRendersTheReceipt()
    {
        // Arrange: the template table does not exist yet, so any query would fail (and abort the transaction on PostgreSQL).
        var templates = new Mock<IBusinessDocumentTemplateManager>(MockBehavior.Strict);
        var renderer = CreateRenderer(templates.Object, ReceiptRenderers.CreateFeatures());

        // Act
        var html = await renderer.RenderAsync(CreateReceipt(), TestContext.Current.CancellationToken);

        // Assert
        templates.VerifyNoOtherCalls();
        Assert.Contains("R-1001", html, StringComparison.Ordinal);
        Assert.Contains("Acme Inc.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_OnceBusinessDocumentsIsEnabled_AppliesTheTemplate_AndLetsItsErrorsThrough()
    {
        // Arrange: a real template error must not be hidden.
        var templates = new Mock<IBusinessDocumentTemplateManager>();
        templates
            .Setup(manager => manager.ApplyAsync(It.IsAny<BusinessDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The template is broken."));

        var renderer = CreateRenderer(templates.Object, ReceiptRenderers.CreateFeatures(BusinessDocumentsConstants.Features.Area));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.RenderAsync(CreateReceipt(), TestContext.Current.CancellationToken));
        templates.Verify(manager => manager.ApplyAsync(It.IsAny<BusinessDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DefaultReceiptHtmlRenderer CreateRenderer(IBusinessDocumentTemplateManager templates, IShellFeaturesManager features)
        => new(
            ReceiptRenderers.CreateMapper(),
            templates,
            new BusinessDocumentHtmlRenderer(HtmlEncoder.Default, new PassThroughStringLocalizer<BusinessDocumentHtmlRenderer>()),
            features);

    private static ReceiptDocument CreateReceipt()
        => new()
        {
            BusinessName = "Acme Inc.",
            SourceLabel = "Receipt",
            Reference = "R-1001",
            Currency = "USD",
            Total = 10m,
            LineItems = [new ReceiptLineItem { Description = "Plan", Quantity = 1, Amount = 10m }],
        };
}
