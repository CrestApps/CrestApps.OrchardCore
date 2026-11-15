using CrestApps.OrchardCore.Pdf;
using CrestApps.OrchardCore.Skills.IssuedCertificates;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Models;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Services;
using CrestApps.OrchardCore.Skills.IssuedCertificates.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Fluid;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Media;
using OrchardCore.Settings;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// The printed certificate (template blocks rendered with Liquid, the QR code of the verification page, a revoked
/// mark), the verification code shown and typed, and the state the verification page shows.
/// </summary>
public sealed class IssuedCertificateDocumentTests
{
    private static readonly Certification _cpr = new() { ItemId = "cpr", Name = "CPR and First Aid", Issuer = "Contoso Training" };

    [Fact]
    public async Task BuildAsync_WithTheDefaultDesign_PrintsTheHolderCertificationDatesNumberAndQrCode()
    {
        // Arrange
        var builder = CreateBuilder(template: null);
        var record = Issued();

        // Act
        using var document = PdfDocument.Open(await builder.BuildAsync(record, _cpr, "https://example.com/certificates/verify/ABCD-EFGH-JKLM-NPQR"));
        var page = document.GetPage(1);
        var text = string.Join(' ', page.GetWords().Select(word => word.Text));

        // Assert
        Assert.Equal(792, page.Width, tolerance: 0.5);
        Assert.Contains("Ana Lima", text);
        Assert.Contains("CPR and First Aid", text);
        Assert.Contains("Contoso Training", text);
        Assert.Contains("CPR-000042", text);
        Assert.Contains("2026", text);
        Assert.Contains("https://example.com/certificates/verify/ABCD-EFGH-JKLM-NPQR", text);
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("Revoked", text);
        Assert.True(page.Paths.Count > 50, $"Expected the QR code squares, but the page has {page.Paths.Count} paths.");
        Assert.Equal("CPR and First Aid CPR-000042", document.Information.Title);
    }

    [Fact]
    public async Task BuildAsync_WithATemplate_UsesItsPageItsBlocksAndLeavesOutTheQrCodeWhenAsked()
    {
        // Arrange
        var template = new CertificateTemplatePart
        {
            PageSize = "A4",
            Landscape = false,
            ShowBorder = false,
            ShowQrCode = false,
            Blocks =
            [
                new CertificateTemplateBlock { Text = "Awarded to {{ Certificate.HolderName | upcase }}", X = 50, Top = 100, FontSize = 20 },
                new CertificateTemplateBlock { Text = "{% if Certificate.ExpiresOn %}Expires {{ Certificate.ExpiresOn }}{% else %}Never expires{% endif %}", X = 50, Top = 160 },
                new CertificateTemplateBlock { Text = "Broken {{ Certificate.HolderName", X = 50, Top = 200 },
            ],
        };
        var builder = CreateBuilder(template);
        var record = Issued();

        record.ExpiresOn = null;

        // Act
        using var document = PdfDocument.Open(await builder.BuildAsync(record, _cpr, "https://example.com/v"));
        var page = document.GetPage(1);
        var text = string.Join(' ', page.GetWords().Select(word => word.Text));

        // Assert
        Assert.Equal(595.28, page.Width, tolerance: 0.5);
        Assert.Contains("Awarded to ANA LIMA", text);
        Assert.Contains("Never expires", text);
        Assert.Contains("Broken {{ Certificate.HolderName", text);
        Assert.Empty(page.Paths);
    }

    [Fact]
    public async Task BuildAsync_ForARevokedCertificate_MarksItRevoked()
    {
        // Arrange
        var builder = CreateBuilder(template: null);
        var record = Issued();

        record.Status = CertificationStatus.Revoked;

        // Act
        using var document = PdfDocument.Open(await builder.BuildAsync(record, _cpr, "https://example.com/v"));

        // Assert
        Assert.Contains("Revoked", document.GetPage(1).Text);
    }

    [Theory]
    [InlineData("ABCDEFGHJKLMNPQR", "ABCD-EFGH-JKLM-NPQR")]
    [InlineData(null, null)]
    public void Format_GroupsTheCodeInFours(string code, string expected)
    {
        Assert.Equal(expected, CertificateVerificationCodes.Format(code));
    }

    [Theory]
    [InlineData(" abcd-efgh jklm-npqr ", "ABCDEFGHJKLMNPQR")]
    [InlineData("ABCD-EFGH-JKLM-NPQ", null)]
    [InlineData("ABCD-EFGH-JKLM-NPQ1", null)]
    [InlineData("", null)]
    public void Normalize_AcceptsWhatPeopleTypeAndRefusesWhatCannotBeACode(string typed, string expected)
    {
        Assert.Equal(expected, CertificateVerificationCodes.Normalize(typed));
    }

    [Fact]
    public void GetState_SaysValidExpiredRevokedOrNotYetValid()
    {
        // Arrange
        var today = new DateOnly(2026, 11, 15);
        var valid = Issued();
        var expired = Issued();
        var revoked = Issued();
        var future = Issued();

        expired.ExpiresOn = today.AddDays(-1);
        revoked.Status = CertificationStatus.Revoked;
        future.IssuedOn = today.AddDays(3);

        // Act and assert
        Assert.Equal(CertificateVerificationState.Valid, CertificateVerificationViewModel.GetState(valid, today));
        Assert.Equal(CertificateVerificationState.Expired, CertificateVerificationViewModel.GetState(expired, today));
        Assert.Equal(CertificateVerificationState.Revoked, CertificateVerificationViewModel.GetState(revoked, today));
        Assert.Equal(CertificateVerificationState.NotYetValid, CertificateVerificationViewModel.GetState(future, today));
    }

    [Fact]
    public void DefaultBlocks_FitInsideALetterPageOnItsSide()
    {
        Assert.All(CertificateTemplateDefaults.CreateBlocks(), block =>
        {
            Assert.InRange(block.Top, 36, 576);
            Assert.InRange(block.X + block.Width, 0, 756);
        });
    }

    private static CertificationRecord Issued()
    {
        return new CertificationRecord
        {
            ItemId = "record",
            SubjectName = "Ana Lima",
            CertificationId = _cpr.ItemId,
            Number = "CPR-000042",
            IssuedOn = new DateOnly(2026, 11, 1),
            ExpiresOn = new DateOnly(2028, 10, 31),
            Evidence = "Completed CPR Basics and passed the final exam.",
            Status = CertificationStatus.Valid,
            VerificationCode = "ABCDEFGHJKLMNPQR",
        };
    }

    private static CertificateDocumentBuilder CreateBuilder(CertificateTemplatePart template)
    {
        var contentManager = new Mock<IContentManager>();

        if (template is not null)
        {
            var item = new ContentItem { ContentItemId = "template", ContentType = "CertificateTemplate" };

            item.Weld(template);
            contentManager.Setup(manager => manager.GetAsync("template", It.IsAny<VersionOptions>())).ReturnsAsync(item);
            _cpr.CertificateTemplateId = "template";
        }
        else
        {
            _cpr.CertificateTemplateId = null;
        }

        var site = new Mock<ISite>();

        site.SetupGet(settings => settings.SiteName).Returns("Contoso");

        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new CertificateDocumentBuilder(
            contentManager.Object,
            LiquidDoubles.Manager(),
            Mock.Of<IMediaFileStore>(),
            new PdfDocumentRenderer(),
            siteService.Object,
            Options.Create(TemplateOptions.Default),
            NullLogger<CertificateDocumentBuilder>.Instance,
            new PassThroughLocalizer());
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<CertificateDocumentBuilder>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
