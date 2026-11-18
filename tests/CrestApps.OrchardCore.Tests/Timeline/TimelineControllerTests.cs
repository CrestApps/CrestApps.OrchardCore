using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Controllers;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.Timeline.ViewComponents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// The timeline card's endpoints: only a kind the composer offers, on a record the user may add to, is stored; a refused
/// entry goes back with what was typed kept; the way back keeps the tenant's path base and never leaves the site; and a
/// redaction needs the permission and the right to read the entry.
/// </summary>
public sealed class TimelineControllerTests
{
    private const string ReturnUrl = "/tenant-a/Admin/work-orders/view/wo1";

    private static readonly RecordRef _workOrder = new("WorkOrder", "wo1");

    [Fact]
    public async Task AKindTheComposerDoesntOffer_IsABadRequest()
    {
        // Arrange
        var (controller, timeline) = Create(canAdd: true);

        // Act
        var result = await controller.Add(_workOrder.ToString(), "Hello", "Reply", ReturnUrl, []);

        // Assert
        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task AUserWhoMayNotAdd_IsForbidden_AndNothingIsStored()
    {
        // Arrange
        var (controller, timeline) = Create(canAdd: false);

        // Act
        var result = await controller.Add(_workOrder.ToString(), "Hello", TimelineConstants.Kinds.Note, ReturnUrl, []);

        // Assert
        Assert.IsType<ForbidResult>(result);
        Assert.Empty(timeline.Entries);
    }

    [Fact]
    public async Task ANote_IsStoredAsTheUsers_AndTheWayBackKeepsTheTenantsPathBase()
    {
        // Arrange
        var (controller, timeline) = Create(canAdd: true);

        // Act
        var result = await controller.Add(_workOrder.ToString(), "Bring the long ladder.", TimelineConstants.Kinds.Note, ReturnUrl, []);

        // Assert
        Assert.Equal(ReturnUrl, Assert.IsType<LocalRedirectResult>(result).Url);
        var entry = Assert.Single(timeline.Entries);
        Assert.Equal(TimelineVisibility.Internal, entry.Visibility);
        Assert.Equal(new PartyRef(PartyKinds.User, "agent-1"), entry.Author);
    }

    [Fact]
    public async Task ARefusedEntry_GoesBack_WithWhatWasTypedKept()
    {
        // Arrange
        var timeline = new Mock<ITimelineService>();
        timeline.Setup(service => service.AddAsync(It.IsAny<TimelineEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineResult { Errors = ["The text is too long."] });
        var (controller, _) = Create(canAdd: true, timelineService: timeline.Object);

        // Act
        var result = await controller.Add(_workOrder.ToString(), "A long reply", TimelineConstants.Kinds.Note, ReturnUrl, []);

        // Assert
        Assert.Equal(ReturnUrl, Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Equal(_workOrder + "\nA long reply", controller.TempData[TimelineViewComponent.DraftKey]);
    }

    [Fact]
    public async Task ARefusedLongText_IsNotKept_SoTheCookieCantOutgrowTheRequestHeaders()
    {
        // Arrange: the most common refusal of all, a body over the length limit.
        var timeline = new Mock<ITimelineService>();
        timeline.Setup(service => service.AddAsync(It.IsAny<TimelineEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimelineResult { Errors = ["The text is too long."] });
        var (controller, _) = Create(canAdd: true, timelineService: timeline.Object);
        var body = new string('x', TimelineConstants.MaxBodyLength + 1);

        // Act
        var result = await controller.Add(_workOrder.ToString(), body, TimelineConstants.Kinds.Note, ReturnUrl, []);

        // Assert
        Assert.Equal(ReturnUrl, Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.False(controller.TempData.ContainsKey(TimelineViewComponent.DraftKey));
    }

    [Fact]
    public async Task AWayBackOffTheSite_GoesToTheAdminInstead()
    {
        // Arrange
        var (controller, _) = Create(canAdd: true);

        // Act
        var result = await controller.Add(_workOrder.ToString(), "Hi", TimelineConstants.Kinds.Note, "https://elsewhere.example/phish", []);

        // Assert
        Assert.Equal("~/Admin", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task Redacting_NeedsThePermission_AndTheRightToReadTheEntry()
    {
        // Arrange
        var timeline = new InMemoryTimelineService();
        await timeline.AddAsync(new TimelineEntry { ItemId = "e1", Subject = _workOrder, Kind = TimelineConstants.Kinds.Note, Visibility = TimelineVisibility.Internal, Body = "Card 4111" }, TestContext.Current.CancellationToken);

        var (withoutPermission, _) = Create(canAdd: true, timelineService: timeline, mayRedact: false, canView: true);
        var (cantRead, _) = Create(canAdd: true, timelineService: timeline, mayRedact: true, canView: false);

        // Act
        var refused = await withoutPermission.Redact("e1", ReturnUrl);
        var hidden = await cantRead.Redact("e1", ReturnUrl);

        // Assert
        Assert.IsType<ForbidResult>(refused);
        Assert.IsType<ForbidResult>(hidden);
        Assert.Equal("Card 4111", (await timeline.FindAsync("e1", TestContext.Current.CancellationToken)).Body);
    }

    private static (TimelineController Controller, InMemoryTimelineService Timeline) Create(
        bool canAdd,
        ITimelineService timelineService = null,
        bool mayRedact = false,
        bool canView = true)
    {
        var timeline = new InMemoryTimelineService();

        var access = new Mock<ITimelineAccessService>();
        access.Setup(service => service.CanAddAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<RecordRef>(), It.IsAny<string>(), It.IsAny<TimelineVisibility>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAdd);
        access.Setup(service => service.CanViewAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<RecordRef>(), It.IsAny<TimelineVisibility>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canView);

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                mayRedact && requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == TimelinePermissions.RedactTimelineEntries.Name)
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var notifier = new Mock<INotifier>();
        notifier.Setup(service => service.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>())).Returns(ValueTask.CompletedTask);
        notifier.Setup(service => service.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>())).Returns(ValueTask.CompletedTask);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "agent-1"), new Claim(ClaimTypes.Name, "agent")], "test")),
        };

        var controller = new TimelineController(
            timelineService ?? timeline,
            access.Object,
            authorization.Object,
            notifier.Object,
            Options.Create(new TimelineOptions()),
            NullLogger<TimelineController>.Instance,
            new PassThroughHtmlLocalizer())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
        };

        var url = new Mock<IUrlHelper>();
        url.Setup(helper => helper.IsLocalUrl(It.IsAny<string>())).Returns<string>(value => !string.IsNullOrEmpty(value) && value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal));
        controller.Url = url.Object;

        return (controller, timeline);
    }

    private sealed class PassThroughHtmlLocalizer : IHtmlLocalizer<TimelineController>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, name, false, arguments);

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
