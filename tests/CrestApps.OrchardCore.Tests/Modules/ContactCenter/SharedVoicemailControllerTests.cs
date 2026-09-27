using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;

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

    private static Harness CreateHarness()
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

        harness.Controller = new SharedVoicemailController(
            service.Object,
            Mock.Of<ISharedVoicemailAuthorizationService>(),
            Mock.Of<IActivityQueueManager>(),
            Mock.Of<IInteractionManager>(),
            Mock.Of<INotifier>(),
            hub.Object,
            new ShellSettings { Name = TenantName },
            NullLogger<SharedVoicemailController>.Instance,
            new NullHtmlLocalizer())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test")),
                },
            },
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
    }
}
