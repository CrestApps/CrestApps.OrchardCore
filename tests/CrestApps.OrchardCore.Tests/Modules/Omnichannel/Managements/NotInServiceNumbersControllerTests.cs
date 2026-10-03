using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Controllers;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

public sealed class NotInServiceNumbersControllerTests
{
    private const string Number = "+17025550123";
    private const string OtherNumber = "+17025550124";

    // The row button used to read "Clear", with a "Dial this number again" tooltip, which looked like it placed a
    // call. It only removes the mark: the list service is asked to clear the number and nothing else is touched.
    [Fact]
    public async Task AllowDialing_RemovesTheMarkAndDialsNothing()
    {
        var harness = CreateHarness();
        harness.Numbers
            .Setup(service => service.ClearAsync(Number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await harness.Controller.AllowDialing(Number, returnUrl: null);

        Assert.IsType<RedirectToActionResult>(result);
        harness.Numbers.Verify(service => service.ClearAsync(Number, It.IsAny<CancellationToken>()), Times.Once);
        harness.Numbers.VerifyNoOtherCalls();

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Success, notice.Type);
        Assert.Equal($"{Number} is no longer marked as not in service. Campaigns can load and dial it again.", notice.Message);
    }

    [Fact]
    public async Task AllowDialing_ANumberThatWasNotMarked_SaysSo()
    {
        var harness = CreateHarness();
        harness.Numbers
            .Setup(service => service.ClearAsync(Number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await harness.Controller.AllowDialing(Number, returnUrl: null);

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Warning, notice.Type);
    }

    [Fact]
    public async Task AllowDialing_WithoutPermission_KeepsTheMark()
    {
        var harness = CreateHarness(authorized: false);

        var result = await harness.Controller.AllowDialing(Number, returnUrl: null);

        Assert.IsType<ForbidResult>(result);
        harness.Numbers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AllowDialing_GoesBackToThePageItWasClickedOn()
    {
        var harness = CreateHarness();
        harness.Numbers
            .Setup(service => service.ClearAsync(Number, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await harness.Controller.AllowDialing(Number, "/Admin/omnichannel/numbers-not-in-service?page=2");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Admin/omnichannel/numbers-not-in-service?page=2", redirect.Url);
    }

    // A selected number is cleared once, even when a page posts it twice.
    [Fact]
    public async Task BulkAllowDialing_RemovesTheMarkFromEverySelectedNumber()
    {
        var harness = CreateHarness();
        harness.Numbers
            .Setup(service => service.ClearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await harness.Controller.IndexPost(Options(NotInServiceNumberBulkAction.AllowDialing, search: "702"), [Number, OtherNumber, Number, " "]);

        harness.Numbers.Verify(service => service.ClearAsync(Number, It.IsAny<CancellationToken>()), Times.Once);
        harness.Numbers.Verify(service => service.ClearAsync(OtherNumber, It.IsAny<CancellationToken>()), Times.Once);
        harness.Numbers.VerifyNoOtherCalls();

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Success, notice.Type);

        // The list comes back with the search it was filtered by.
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(NotInServiceNumbersController.Index), redirect.ActionName);
        Assert.Equal("702", redirect.RouteValues["Options.Search"]);
    }

    [Fact]
    public async Task BulkAllowDialing_WhenNoSelectedNumberWasMarked_Warns()
    {
        var harness = CreateHarness();
        harness.Numbers
            .Setup(service => service.ClearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await harness.Controller.IndexPost(Options(NotInServiceNumberBulkAction.AllowDialing), [Number, OtherNumber]);

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Warning, notice.Type);
    }

    [Fact]
    public async Task BulkAllowDialing_WithoutPermission_KeepsEveryMark()
    {
        var harness = CreateHarness(authorized: false);

        var result = await harness.Controller.IndexPost(Options(NotInServiceNumberBulkAction.AllowDialing), [Number, OtherNumber]);

        Assert.IsType<ForbidResult>(result);
        harness.Numbers.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BulkAction_None_ChangesNothing()
    {
        var harness = CreateHarness();

        var result = await harness.Controller.IndexPost(Options(NotInServiceNumberBulkAction.None), [Number]);

        Assert.IsType<RedirectToActionResult>(result);
        harness.Numbers.VerifyNoOtherCalls();
        Assert.Empty(harness.Notices);
    }

    [Fact]
    public async Task BulkAction_ThatIsNotKnown_IsRefused()
    {
        var harness = CreateHarness();

        var result = await harness.Controller.IndexPost(Options((NotInServiceNumberBulkAction)99), [Number]);

        Assert.IsType<BadRequestResult>(result);
        harness.Numbers.VerifyNoOtherCalls();
    }

    // The page offers exactly the bulk actions the post handles, so no menu item ends in a bad request.
    [Fact]
    public async Task Index_OffersAllowDialingAsTheBulkAction()
    {
        var harness = CreateHarness();
        harness.Numbers
            .Setup(service => service.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<NotInServiceNumber>
            {
                Count = 1,
                Entries = [new NotInServiceNumber { ItemId = "mark-1", PhoneNumber = Number }],
            });

        var result = await harness.Controller.Index(Options(NotInServiceNumberBulkAction.None, search: "702"), new PagerParameters(), Microsoft.Extensions.Options.Options.Create(new PagerOptions()), harness.ShapeFactory);

        var model = Assert.IsType<NotInServiceNumbersIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        var action = Assert.Single(model.Options.BulkActions);
        Assert.Equal(nameof(NotInServiceNumberBulkAction.AllowDialing), action.Value);
        Assert.Equal("702", model.Options.Search);
        Assert.Equal(Number, Assert.Single(model.Entries).Number.PhoneNumber);
    }

    private static CatalogEntryOptions<NotInServiceNumberBulkAction> Options(NotInServiceNumberBulkAction action, string search = null)
        => new()
        {
            BulkAction = action,
            Search = search,
        };

    private static Harness CreateHarness(bool authorized = true)
    {
        var harness = new Harness();

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var notifier = new Mock<INotifier>();
        notifier
            .Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Callback((NotifyType type, LocalizedHtmlString message) => harness.Notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);
        notifier
            .Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>()))
            .Callback((NotifyType type, LocalizedHtmlString message, NotifyContext _) => harness.Notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);

        // No listed mark names a campaign, so the campaigns are never read.
        var campaigns = new Mock<ICatalog<OmnichannelCampaign>>(MockBehavior.Strict);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
        };

        harness.Controller = new NotInServiceNumbersController(
            harness.Numbers.Object,
            campaigns.Object,
            authorization.Object,
            notifier.Object,
            new TestHtmlLocalizer(),
            new TestStringLocalizer())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
            Url = new UrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor())),
        };

        return harness;
    }

    private sealed class TestHtmlLocalizer : IHtmlLocalizer<NotInServiceNumbersController>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class TestStringLocalizer : IStringLocalizer<NotInServiceNumbersController>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class Harness
    {
        public Mock<INotInServiceNumberService> Numbers { get; } = new(MockBehavior.Strict);

        public NotInServiceNumbersController Controller { get; set; }

        public List<(NotifyType Type, string Message)> Notices { get; } = [];

        // Only the pager is built from it, and the pager's shape is not what these tests read.
        public IShapeFactory ShapeFactory { get; } = new Mock<IShapeFactory> { DefaultValue = DefaultValue.Mock }.Object;
    }
}
