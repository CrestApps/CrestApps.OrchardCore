using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class SharedVoicemailControllerTests
{
    private const string TenantName = "Default";
    private const string UserId = "user-1";

    // The badge on the Shared voicemail menu item reads this on every admin page.
    [Fact]
    public async Task Count_ReturnsHowManyNewVoicemailsTheUserCanSee()
    {
        var harness = CreateHarness();
        SharedVoicemailQuery asked = null;
        harness.Service
            .Setup(service => service.ListAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<SharedVoicemailQuery>(), It.IsAny<CancellationToken>()))
            .Callback((ClaimsPrincipal _, SharedVoicemailQuery query, CancellationToken _) => asked = query)
            .ReturnsAsync(new SharedVoicemailPage { Count = 3 });

        var result = Assert.IsType<JsonResult>(await harness.Controller.Count());

        Assert.Equal(3, (int)result.Value.GetType().GetProperty("count").GetValue(result.Value));
        Assert.NotNull(asked);
        Assert.Equal(SharedVoicemailStatus.New, asked.Status);

        // Only the total is needed, so no page of voicemails is read for it.
        Assert.Equal(1, asked.PageSize);
    }

    // Live, a call-back was queued as a preview call the user then had to wait for and dial again. It now dials at once,
    // from the user's own soft phone.
    [Fact]
    public async Task CallBack_AsksTheUsersOwnSoftPhoneToDialTheCaller()
    {
        var harness = CreateHarness();
        harness.Service
            .Setup(service => service.RequestCallbackAsync(It.IsAny<ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Success(new SharedVoicemail { ItemId = "vm-1", CallerNumber = "+15550100", QueueId = "queue-1" }));

        await harness.Controller.CallBack("vm-1", returnUrl: null);

        Assert.Equal(TenantSignalRGroupName.ForUser(TenantName, UserId), harness.DialedGroup);
        harness.Client.Verify(client => client.DialRequested(It.Is<TelephonyDialRequest>(request => request.Number == "+15550100")), Times.Once);
    }

    [Fact]
    public async Task CallBack_WhenRefused_DialsNothing()
    {
        var harness = CreateHarness();
        harness.Service
            .Setup(service => service.RequestCallbackAsync(It.IsAny<ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, reasonCode: SharedVoicemailReasons.CallbackAlreadyStarted));

        await harness.Controller.CallBack("vm-1", returnUrl: null);

        harness.Client.Verify(client => client.DialRequested(It.IsAny<TelephonyDialRequest>()), Times.Never);
    }

    // Live, deleting a message whose recording is under legal hold kept the message; the page has to say why, or the
    // user clicks Delete again and again.
    [Fact]
    public async Task Delete_ARecordingUnderLegalHold_SaysSo()
    {
        var harness = CreateHarness();
        harness.Service
            .Setup(service => service.DeleteAsync(It.IsAny<ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Failure(SharedVoicemailActionStatus.Conflict, new SharedVoicemail { ItemId = "vm-1" }, SharedVoicemailReasons.LegalHold));

        await harness.Controller.Delete("vm-1", returnUrl: null);

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Error, notice.Type);
        Assert.Equal("The voicemail's recording is under legal hold and cannot be deleted.", notice.Message);
    }

    [Fact]
    public async Task Release_WhenItSucceeds_SaysTheMessageIsBackWithTheQueue()
    {
        var harness = CreateHarness();
        harness.Service
            .Setup(service => service.ReleaseAsync(It.IsAny<ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Success(new SharedVoicemail { ItemId = "vm-1", Status = SharedVoicemailStatus.New }));

        await harness.Controller.Release("vm-1", returnUrl: null);

        var notice = Assert.Single(harness.Notices);
        Assert.Equal(NotifyType.Success, notice.Type);
        Assert.Equal("The voicemail was returned to the queue.", notice.Message);
    }

    // A returnUrl is posted by the page, so anything can be put in it: only a URL on this site is followed.
    [Theory]
    [InlineData("https://phishing.example/contact-center")]
    [InlineData("//phishing.example/contact-center")]
    public async Task Release_WithAReturnUrlOffTheSite_GoesBackToTheVoicemailList(string returnUrl)
    {
        var harness = CreateHarness();
        harness.Service
            .Setup(service => service.ReleaseAsync(It.IsAny<ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Success(new SharedVoicemail { ItemId = "vm-1" }));

        var result = await harness.Controller.Release("vm-1", returnUrl);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("ContactCenterSharedVoicemailIndex", redirect.RouteName);
    }

    [Fact]
    public async Task Release_WithAReturnUrlOnTheSite_GoesBackToIt()
    {
        var harness = CreateHarness();
        harness.Service
            .Setup(service => service.ReleaseAsync(It.IsAny<ClaimsPrincipal>(), "vm-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedVoicemailActionResult.Success(new SharedVoicemail { ItemId = "vm-1" }));

        var result = await harness.Controller.Release("vm-1", "/Admin/contact-center/shared-voicemail?status=Claimed");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Admin/contact-center/shared-voicemail?status=Claimed", redirect.Url);
    }

    // Each filter on the page reads the messages it names; "Open", the page's default, is every message not yet dealt
    // with, which is what a team works from.
    [Theory]
    [InlineData(SharedVoicemailStatusFilter.Open, null, false)]
    [InlineData(SharedVoicemailStatusFilter.New, SharedVoicemailStatus.New, false)]
    [InlineData(SharedVoicemailStatusFilter.Claimed, SharedVoicemailStatus.Claimed, false)]
    [InlineData(SharedVoicemailStatusFilter.Resolved, SharedVoicemailStatus.Resolved, false)]
    [InlineData(SharedVoicemailStatusFilter.All, null, true)]
    public void ApplyStatus_ReadsTheMessagesTheFilterNames(SharedVoicemailStatusFilter filter, SharedVoicemailStatus? status, bool includeResolved)
    {
        var query = new SharedVoicemailQuery();

        SharedVoicemailController.ApplyStatus(query, filter);

        Assert.Equal(status, query.Status);
        Assert.Equal(includeResolved, query.IncludeResolved);
    }

    [Fact]
    public void ApplyStatus_CoversEveryFilterThePageOffers()
    {
        // A filter added to the page without a case here would silently read the Open list.
        var covered = new[]
        {
            SharedVoicemailStatusFilter.Open,
            SharedVoicemailStatusFilter.New,
            SharedVoicemailStatusFilter.Claimed,
            SharedVoicemailStatusFilter.Resolved,
            SharedVoicemailStatusFilter.All,
        };

        Assert.Equal(covered.Order(), Enum.GetValues<SharedVoicemailStatusFilter>().Order());
    }

    // A queue filter in the address naming another team's queue is dropped rather than obeyed, so the page shows the
    // user's own boxes instead of an empty list that looks like the other team has no messages.
    [Fact]
    public async Task Index_WithAQueueTheUserCannotSee_ListsEveryQueueTheyCan()
    {
        var harness = CreateHarness(Access("queue-main"));
        var asked = CaptureListQueries(harness);

        var result = await harness.Controller.Index("queue-billing", SharedVoicemailStatusFilter.Open, new PagerParameters(), Options.Create(new PagerOptions()), harness.ShapeFactory);

        var model = Assert.IsType<SharedVoicemailListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(model.QueueId);
        Assert.Null(asked[0].QueueIds);
        Assert.DoesNotContain(model.QueueOptions, option => option.Value == "queue-billing");
    }

    [Fact]
    public async Task Index_WithAQueueTheUserCanSee_ListsThatQueue()
    {
        var harness = CreateHarness(Access("queue-main"));
        var asked = CaptureListQueries(harness);

        var result = await harness.Controller.Index("queue-main", SharedVoicemailStatusFilter.Resolved, new PagerParameters(), Options.Create(new PagerOptions()), harness.ShapeFactory);

        var model = Assert.IsType<SharedVoicemailListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("queue-main", model.QueueId);
        Assert.Equal(["queue-main"], asked[0].QueueIds);
        Assert.Equal(SharedVoicemailStatus.Resolved, asked[0].Status);
    }

    private static SharedVoicemailAccess Access(params string[] queueIds)
        => new()
        {
            UserId = UserId,
            UserName = "agent.one",
            CanAccess = true,
            QueueIds = queueIds,
        };

    private static List<SharedVoicemailQuery> CaptureListQueries(Harness harness)
    {
        var asked = new List<SharedVoicemailQuery>();
        harness.Service
            .Setup(service => service.ListAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<SharedVoicemailQuery>(), It.IsAny<CancellationToken>()))
            .Callback((ClaimsPrincipal _, SharedVoicemailQuery query, CancellationToken _) => asked.Add(query))
            .ReturnsAsync(new SharedVoicemailPage());

        return asked;
    }

    private static Harness CreateHarness(SharedVoicemailAccess access = null)
    {
        var service = new Mock<ISharedVoicemailService>();
        var client = new Mock<ITelephonyClient>();
        var harness = new Harness { Service = service, Client = client };

        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients
            .Setup(value => value.Group(It.IsAny<string>()))
            .Callback((string group) => harness.DialedGroup = group)
            .Returns(client.Object);

        var hub = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        hub.SetupGet(value => value.Clients).Returns(clients.Object);

        var notifier = new Mock<INotifier>();
        notifier
            .Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Callback((NotifyType type, LocalizedHtmlString message) => harness.Notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);
        notifier
            .Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>()))
            .Callback((NotifyType type, LocalizedHtmlString message, NotifyContext _) => harness.Notices.Add((type, message.Value)))
            .Returns(ValueTask.CompletedTask);

        var authorization = new Mock<ISharedVoicemailAuthorizationService>();
        authorization
            .Setup(value => value.GetAccessAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(access ?? SharedVoicemailAccess.None);

        var queues = new Mock<IActivityQueueManager>();
        queues
            .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ActivityQueue { ItemId = "queue-main", Name = "Main line" },
                new ActivityQueue { ItemId = "queue-billing", Name = "Billing" },
            ]);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test")),
        };

        harness.Controller = new SharedVoicemailController(
            service.Object,
            authorization.Object,
            queues.Object,
            Mock.Of<IInteractionManager>(),
            notifier.Object,
            hub.Object,
            new ShellSettings { Name = TenantName },
            NullLogger<SharedVoicemailController>.Instance,
            new NullHtmlLocalizer())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },

            // The framework's own local-URL check, so what counts as on the site is ASP.NET Core's answer.
            Url = new UrlHelper(new ActionContext(httpContext, new RouteData(), new ActionDescriptor())),
        };

        return harness;
    }

    private sealed class NullHtmlLocalizer : IHtmlLocalizer<SharedVoicemailController>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class Harness
    {
        public Mock<ISharedVoicemailService> Service { get; init; }

        public Mock<ITelephonyClient> Client { get; init; }

        public SharedVoicemailController Controller { get; set; }

        public string DialedGroup { get; set; }

        public List<(NotifyType Type, string Message)> Notices { get; } = [];

        // Only the pager is built from it, and the pager's shape is not what these tests read.
        public IShapeFactory ShapeFactory { get; } = new Mock<IShapeFactory> { DefaultValue = DefaultValue.Mock }.Object;
    }
}
