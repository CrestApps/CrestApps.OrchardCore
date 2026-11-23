using System.Text;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Controllers;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.Consent.ViewModels;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Consent;

/// <summary>
/// The e-mail preferences page: opening a link changes nothing, a mail app's RFC 8058 one-click post stops the link's
/// scope without a form token, and "stop all marketing e-mail" records a marketing opt-out for the address.
/// </summary>
public sealed class EmailPreferencesControllerTests
{
    private const string Address = "sam@example.com";

    private readonly UnsubscribeLinks _links;
    private readonly Mock<IConsentService> _consent = new();
    private readonly Mock<IUnsubscribeScopeHandler> _listHandler = new();

    public EmailPreferencesControllerTests()
    {
        var site = new Mock<ISite>();
        site.SetupGet(value => value.BaseUrl).Returns("https://shop.example.com");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        _links = new UnsubscribeLinks(new SignedTokenService(new EphemeralDataProtectionProvider(), new TestClock(DateTime.UtcNow)), siteService.Object);

        _listHandler.SetupGet(handler => handler.Kind).Returns("MailingList");
        _listHandler.Setup(handler => handler.GetTitleAsync(It.IsAny<UnsubscribeScope>(), It.IsAny<CancellationToken>())).ReturnsAsync("Weekly news");
    }

    [Fact]
    public async Task OneClick_AnAllMarketingLink_RecordsAMarketingOptOutForTheAddress()
    {
        // Arrange
        var controller = Controller(OneClickBody());

        // Act
        var result = await controller.OneClick(await TokenAsync(UnsubscribeScope.AllMarketing));

        // Assert
        Assert.IsType<OkResult>(result);
        _consent.Verify(consent => consent.RecordAsync(
            ConsentChannels.Email,
            Address,
            It.Is<ConsentChange>(change => change.OptedOut && change.Purpose == ConsentPurposes.Marketing && change.Source == EmailPreferencesController.ConsentSource),
            It.IsAny<CancellationToken>()), Times.Once());
        Assert.Equal("noindex, nofollow", controller.Response.Headers["X-Robots-Tag"].ToString());
    }

    [Fact]
    public async Task OneClick_AListLink_UnsubscribesTheList_AndNotAllMarketing()
    {
        // Arrange
        var controller = Controller(OneClickBody());

        // Act
        var result = await controller.OneClick(await TokenAsync(new UnsubscribeScope { Kind = "MailingList", Id = "list-1" }));

        // Assert
        Assert.IsType<OkResult>(result);
        _listHandler.Verify(handler => handler.UnsubscribeAsync(It.Is<UnsubscribeTicket>(ticket => ticket.Address == Address && ticket.Scope.Id == "list-1"), It.IsAny<CancellationToken>()), Times.Once());
        _consent.Verify(consent => consent.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConsentChange>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task OneClick_AKindNoFeatureHandles_StopsAllMarketing()
    {
        // Arrange
        var controller = Controller(OneClickBody());

        // Act
        var result = await controller.OneClick(await TokenAsync(new UnsubscribeScope { Kind = "RetiredKind", Id = "x" }));

        // Assert
        Assert.IsType<OkResult>(result);
        _consent.Verify(consent => consent.RecordAsync(ConsentChannels.Email, Address, It.Is<ConsentChange>(change => change.Purpose == ConsentPurposes.Marketing), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Theory]
    [InlineData("List-Unsubscribe=Later")]
    [InlineData("")]
    public async Task OneClick_WithoutTheOneClickBody_IsRefused_AndChangesNothing(string body)
    {
        // Arrange
        var controller = Controller(body);

        // Act
        var result = await controller.OneClick(await TokenAsync(UnsubscribeScope.AllMarketing));

        // Assert
        Assert.IsType<BadRequestResult>(result);
        _consent.Verify(consent => consent.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConsentChange>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task OneClick_AForgedToken_IsRefused()
    {
        // Act
        var result = await Controller(OneClickBody()).OneClick("forged");

        // Assert
        Assert.IsType<BadRequestResult>(result);
        _consent.Verify(consent => consent.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConsentChange>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Index_ShowsTheScope_AndChangesNothing()
    {
        // Arrange
        var controller = Controller();

        // Act
        var result = await controller.Index(await TokenAsync(new UnsubscribeScope { Kind = "MailingList", Id = "list-1" }));

        // Assert
        var model = Assert.IsType<EmailPreferencesViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.True(model.IsValid);
        Assert.True(model.CanUnsubscribeScope);
        Assert.Equal("Weekly news", model.ScopeTitle);
        Assert.Equal(Address, model.Email);
        _listHandler.Verify(handler => handler.UnsubscribeAsync(It.IsAny<UnsubscribeTicket>(), It.IsAny<CancellationToken>()), Times.Never());
        _consent.Verify(consent => consent.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConsentChange>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Index_AForgedToken_ShowsTheLinkDoesNotWork()
    {
        // Act
        var result = await Controller().Index("forged");

        // Assert
        var model = Assert.IsType<EmailPreferencesViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.False(model.IsValid);
    }

    [Fact]
    public async Task Update_StopAll_UnsubscribesTheList_AndRecordsAMarketingOptOut()
    {
        // Arrange
        var controller = Controller();

        // Act
        var result = await controller.Update(await TokenAsync(new UnsubscribeScope { Kind = "MailingList", Id = "list-1" }), stopAll: true);

        // Assert
        var model = Assert.IsType<EmailPreferencesViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.True(model.StoppedAllMarketing);
        _listHandler.Verify(handler => handler.UnsubscribeAsync(It.IsAny<UnsubscribeTicket>(), It.IsAny<CancellationToken>()), Times.Once());
        _consent.Verify(consent => consent.RecordAsync(ConsentChannels.Email, Address, It.Is<ConsentChange>(change => change.OptedOut && change.Purpose == ConsentPurposes.Marketing), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Update_TheListOnly_DoesNotStopAllMarketing()
    {
        // Arrange
        var controller = Controller();

        // Act
        var result = await controller.Update(await TokenAsync(new UnsubscribeScope { Kind = "MailingList", Id = "list-1" }), stopAll: false);

        // Assert
        var model = Assert.IsType<EmailPreferencesViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.True(model.Unsubscribed);
        Assert.False(model.StoppedAllMarketing);
        _listHandler.Verify(handler => handler.UnsubscribeAsync(It.IsAny<UnsubscribeTicket>(), It.IsAny<CancellationToken>()), Times.Once());
        _consent.Verify(consent => consent.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConsentChange>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private EmailPreferencesController Controller(string formBody = null)
    {
        var httpContext = new DefaultHttpContext();

        if (formBody is not null)
        {
            httpContext.Request.Method = HttpMethods.Post;
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(formBody));
        }

        return new EmailPreferencesController(
            _links,
            [_listHandler.Object],
            _consent.Object,
            NullLogger<EmailPreferencesController>.Instance,
            new PassThroughStringLocalizer<EmailPreferencesController>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private async Task<string> TokenAsync(UnsubscribeScope scope)
    {
        var url = await _links.CreateUrlAsync(Address, scope, TestContext.Current.CancellationToken);

        return Uri.UnescapeDataString(url.AsSpan(url.IndexOf("?token=", StringComparison.Ordinal) + "?token=".Length));
    }

    private static string OneClickBody()
        => "List-Unsubscribe=One-Click";
}
