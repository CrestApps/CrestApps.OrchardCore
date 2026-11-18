using System.Globalization;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

public sealed class BusinessDocumentHtmlRendererTests
{
    private const string Hostile = "<script>alert(1)</script>";

    [Fact]
    public async Task RenderAsync_EncodesEveryValueTakenFromTheDocument()
    {
        // Arrange
        var document = new BusinessDocument
        {
            Title = Hostile,
            Number = Hostile,
            Watermark = Hostile,
            Status = new BusinessDocumentStatus(Hostile, BusinessDocumentTone.Danger),
            Issuer = new BusinessDocumentParty { Name = Hostile, Lines = [Hostile], TaxId = Hostile, Email = Hostile },
            Recipient = new BusinessDocumentParty { Name = Hostile, Lines = [Hostile], Phone = Hostile },
            ShipTo = new BusinessDocumentParty { Name = Hostile, Website = Hostile },
            RecipientTitle = Hostile,
            Fields = [new BusinessDocumentField(Hostile, Hostile)],
            Columns = [new BusinessDocumentColumn("Name", Hostile)],
            Rows =
            [
                new BusinessDocumentRow { Cells = { ["Name"] = Hostile }, Detail = Hostile },
                new BusinessDocumentRow { Kind = BusinessDocumentRowKind.Section, Cells = { ["Name"] = Hostile } },
            ],
            Totals = [new BusinessDocumentTotal(Hostile, Hostile, emphasized: true)],
            Sections = [new BusinessDocumentSection(Hostile, Hostile)],
            Footer = Hostile,
            AccentColor = "red;background:url(javascript:alert(1))",
            LogoUrl = "javascript:alert(1)",
        };

        // Act
        var html = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_WritesTheDocumentsContent()
    {
        // Arrange
        var document = BusinessDocumentSamples.CreateInvoice();

        // Act
        var html = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        foreach (var expected in new[] { "INV-1001", "Contoso Supplies", "Fabrikam Outfitters", "Fabrikam Warehouse", "Due date", "May 31, 2026", "Item1", "Item3", "Amount due", "$97.20", "Payment instructions", "Thank you for your business.", "Unpaid" })
        {
            Assert.Contains(expected, html, StringComparison.Ordinal);
        }

        Assert.Contains("#2563EB", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_LeavesOutTheBlocksTheDocumentDoesNotHave()
    {
        // Arrange
        var document = new BusinessDocument { Title = "Receipt" };

        // Act
        var html = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Receipt", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Bill to", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Ship to", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<th", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("text-transform:uppercase;padding:2px 10px", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_PrintsTheLocalizedDefaultTitles_OnlyWhereTheDocumentLeavesThemNull()
    {
        // Arrange
        var document = BusinessDocumentSamples.CreateInvoice();
        document.ShipToTitle = "Deliver to";

        // Act
        var html = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Bill to", html, StringComparison.Ordinal);
        Assert.Contains("Deliver to", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Ship to", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_RendersInTheDocumentsCulture_AndRestoresTheCurrentOne()
    {
        // Arrange
        var renderer = new BusinessDocumentHtmlRenderer(HtmlEncoder.Default, new CultureNamingLocalizer());
        var document = BusinessDocumentSamples.CreateInvoice();
        document.Culture = "fr-FR";

        var before = CultureInfo.CurrentUICulture.Name;

        // Act
        var html = await renderer.RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Bill to (fr-FR)", html, StringComparison.Ordinal);
        Assert.Equal(before, CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public async Task RenderAsync_IgnoresACultureThatDoesNotExist()
    {
        // Arrange
        var document = BusinessDocumentSamples.CreateInvoice();
        document.Culture = "not-a-culture-name";

        // Act
        var html = await CreateRenderer().RenderAsync(document, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("INV-1001", html, StringComparison.Ordinal);
    }

    private static BusinessDocumentHtmlRenderer CreateRenderer()
        => new(HtmlEncoder.Default, new PassThroughStringLocalizer<BusinessDocumentHtmlRenderer>());

    private sealed class CultureNamingLocalizer : IStringLocalizer<BusinessDocumentHtmlRenderer>
    {
        public LocalizedString this[string name]
            => new(name, $"{name} ({CultureInfo.CurrentUICulture.Name})");

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.CurrentCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
