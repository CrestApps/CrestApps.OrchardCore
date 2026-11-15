using CrestApps.Core.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Portal;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The portal's pages, through the controller: a member's request is filed under their account; a refused reply comes
/// back on the page with its text; a guest's link is traded for a cookie and the page reloads without it, and a link
/// for an earlier requester opens nothing; every page is private (no cache, no referrer, no index).
/// </summary>
public sealed class TicketPortalControllerTests
{
    [Theory]
    [InlineData(TicketDeskAudience.Customers, "acme")]
    [InlineData(TicketDeskAudience.Internal, null)]
    public async Task AMembersRequest_IsFiledUnderTheirAccount_OnADeskThatServesCustomers(TicketDeskAudience audience, string accountId)
    {
        // Arrange: Ben is a plain member of Acme, and staff (so internal desks are offered too).
        var kit = new Kit("u-ben", audience);
        TicketCreateRequest created = null;
        kit.Tickets.Setup(service => service.CreateAsync(It.IsAny<TicketCreateRequest>(), It.IsAny<CancellationToken>()))
            .Callback((TicketCreateRequest request, CancellationToken _) => created = request)
            .ReturnsAsync(new TicketResult { Ticket = kit.Ticket });

        // Act
        var result = await kit.Controller.CreatePost(new TicketPortalCreateViewModel { ContentType = "Request", DeskId = "desk", Body = "My laptop won't charge." }, files: null);

        // Assert
        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal(TicketPortalController.DisplayRouteName, redirect.RouteName);
        Assert.Equal(accountId, created.AccountId);
        Assert.Equal(new PartyRef(PartyKinds.User, "u-ben"), created.Requester);
    }

    [Fact]
    public async Task ARefusedReply_ComesBackOnThePage_WithItsText()
    {
        // Arrange
        var kit = new Kit("u-ben", TicketDeskAudience.Customers);
        kit.Tickets.Setup(service => service.ReplyAsync("t1", It.IsAny<TicketReply>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult { Errors = ["The message is too long."] });

        // Act
        var result = await kit.Controller.Reply("T-1", "Here is more detail.", files: null);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TicketPortalDisplayViewModel>(view.Model);
        Assert.Equal("The message is too long.", model.Error);
        Assert.Equal("Here is more detail.", model.Draft);
        Assert.Equal("no-store", kit.Controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task ASentReply_SaysSoOnThePage()
    {
        // Arrange
        var kit = new Kit("u-ben", TicketDeskAudience.Customers);
        kit.Tickets.Setup(service => service.ReplyAsync("t1", It.IsAny<TicketReply>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult { Ticket = kit.Ticket });

        // Act
        var redirect = Assert.IsType<RedirectToRouteResult>(await kit.Controller.Reply("T-1", "Thanks!", files: null));
        var page = Assert.IsType<ViewResult>(await kit.Controller.Display("T-1", (string)redirect.RouteValues["done"]));

        // Assert
        Assert.Equal("Your reply was sent.", Assert.IsType<TicketPortalDisplayViewModel>(page.Model).Message);
    }

    [Fact]
    public async Task AGuestsLink_IsTradedForACookie_AndALinkForAnEarlierRequesterOpensNothing()
    {
        // Arrange: a guest's ticket.
        var kit = new Kit(null, TicketDeskAudience.Customers, guestEmail: "dana@contoso.com");
        var token = kit.Access.CreateGuestToken(kit.Ticket);

        // Act
        var first = await kit.Controller.Guest("t1", token);
        var setCookie = kit.Controller.Response.Headers.SetCookie.ToString();
        var withoutCookie = await kit.Controller.Guest("t1", token: null);
        kit.Controller.Request.Headers.Cookie = "tickets-guest=" + Uri.EscapeDataString(token);
        var withCookie = await kit.Controller.Guest("t1", token: null);
        kit.Ticket.Alter<TicketPart>(part => part.RequesterContact = new ContactInfo { Name = "Lee", Email = "lee@contoso.com" });
        var afterRequesterChange = await kit.Controller.Guest("t1", token: null);

        // Assert
        var redirect = Assert.IsType<RedirectToRouteResult>(first);
        Assert.False(redirect.RouteValues.ContainsKey("token"));
        Assert.Contains("tickets-guest=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/requests/t1", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<NotFoundResult>(withoutCookie);
        Assert.True(Assert.IsType<TicketPortalDisplayViewModel>(Assert.IsType<ViewResult>(withCookie).Model).IsGuest);
        Assert.IsType<NotFoundResult>(afterRequesterChange);
        Assert.Equal("no-referrer", kit.Controller.Response.Headers["Referrer-Policy"].ToString());
    }

    [Fact]
    public void EveryRequesterPost_IsRateLimited_AndAFormWithFilesHasASizeLimit()
    {
        var posts = new[] { typeof(TicketPortalController), typeof(CrestApps.OrchardCore.Tickets.WebForm.TicketFormController) }
            .SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: false).Length > 0)
            .ToList();

        Assert.NotEmpty(posts);
        Assert.All(posts, method => Assert.True(method.GetCustomAttributes(typeof(global::OrchardCore.RateLimits.RateLimitGroupAttribute), inherit: false).Length > 0, method.Name + " has no rate-limit group."));
        Assert.All(posts.Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(List<IFormFile>))), method =>
            Assert.True(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: false).Length > 0, method.Name + " has no size limit."));
    }

    private sealed class Kit
    {
        public Kit(string userId, TicketDeskAudience audience, string guestEmail = null)
        {
            Ticket = new ContentItem { ContentItemId = "t1", ContentType = "Request", DisplayText = "Laptop" };
            Ticket.Apply(new TicketPart
            {
                Number = "T-1",
                DeskId = "desk",
                Requester = userId is null ? default : new PartyRef(PartyKinds.User, userId),
                RequesterContact = guestEmail is null ? null : new ContactInfo { Name = "Dana", Email = guestEmail },
            });

            Tickets.Setup(service => service.FindByNumberAsync("T-1")).ReturnsAsync(() => Ticket);
            Tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(() => Ticket);

            var desk = new TicketDesk { ItemId = "desk", Name = "Help", Audience = audience };
            var desks = new Mock<ICatalog<TicketDesk>>();
            desks.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([desk]);
            desks.Setup(value => value.FindByIdAsync("desk", It.IsAny<CancellationToken>())).ReturnsAsync(desk);
            var statuses = new Mock<ICatalog<TicketStatus>>();
            statuses.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var members = new Mock<IAccountMemberService>();
            members.Setup(value => value.FindAccountAsync("u-ben", It.IsAny<CancellationToken>())).ReturnsAsync(() =>
            {
                var account = new ContentItem { ContentItemId = "acme", DisplayText = "Acme" };
                account.Apply(new AccountMembersPart { Members = [new AccountMember { UserId = "u-ben", Role = AccountMemberRole.Member }] });

                return account;
            });
            var staff = new Mock<CrestApps.OrchardCore.Staff.Services.IStaffDirectory>();
            staff.Setup(value => value.IsStaffAsync("u-ben", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc));
            var timeline = new Mock<ITimelineService>();
            timeline.Setup(value => value.ListAsync(It.IsAny<TimelineQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TimelinePage([], 0));

            var services = new ServiceCollection()
                .AddSingleton(clock.Object)
                .AddSingleton(staff.Object)
                .BuildServiceProvider();
            Access = new TicketPortalAccess(Mock.Of<global::YesSql.ISession>(), [members.Object], new SignedTokenService(new EphemeralDataProtectionProvider(), clock.Object), desks.Object, services);

            var requestType = new ContentTypeDefinition("Request", "Request", [new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName, [], []), new System.Text.Json.Nodes.JsonObject
            {
                [nameof(TicketPartSettings)] = System.Text.Json.JsonSerializer.SerializeToNode(new TicketPartSettings { CustomerSubmittable = true }),
            })], []);
            var definitions = new Mock<IContentDefinitionManager>();
            definitions.Setup(value => value.ListTypeDefinitionsAsync()).ReturnsAsync([requestType]);
            definitions.Setup(value => value.GetTypeDefinitionAsync("Request")).ReturnsAsync(requestType);
            var contentManager = new Mock<IContentManager>();
            contentManager.Setup(value => value.NewAsync("Request")).ReturnsAsync(() => new ContentItem { ContentType = "Request" });
            var display = new Mock<IContentItemDisplayManager>();
            display.Setup(value => value.UpdateEditorAsync(It.IsAny<ContentItem>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Mock.Of<IShape>());
            var localClock = new Mock<ILocalClock>();
            localClock.Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);
            var html = new Mock<IHtmlLocalizer<TicketPortalController>>();
            html.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
            html.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] arguments) => new LocalizedHtmlString(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments)));

            Controller = new TicketPortalController(
                Access,
                Tickets.Object,
                timeline.Object,
                desks.Object,
                statuses.Object,
                contentManager.Object,
                definitions.Object,
                display.Object,
                Mock.Of<IUpdateModelAccessor>(),
                new TicketUploads([], Options.Create(new AttachmentOptions()), Doubles.RecipeRoundTrip.Localizer<TicketUploads>()),
                Array.Empty<IAttachmentService>(),
                localClock.Object,
                Mock.Of<INotifier>(),
                html.Object);

            var httpContext = new DefaultHttpContext
            {
                User = userId is null ? new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()) : CalendarTestHarness.User(userId),
                RequestServices = services,
            };
            Controller.ControllerContext = new ControllerContext(new Microsoft.AspNetCore.Mvc.ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()));
            Controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            Controller.Url = Mock.Of<Microsoft.AspNetCore.Mvc.IUrlHelper>();

            // The headers a real request would get from the action filter.
            Controller.OnActionExecuting(new Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext(Controller.ControllerContext, [], new Dictionary<string, object>(), Controller));
        }

        public Mock<ITicketService> Tickets { get; } = new();

        public ContentItem Ticket { get; }

        public TicketPortalAccess Access { get; }

        public TicketPortalController Controller { get; }
    }
}
