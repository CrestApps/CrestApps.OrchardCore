using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A supervisor on an agent's own phone call -- a number dialed from the keypad, or an extension call at either end.
/// It has no interaction or call session: the call is found in the soft phone's call history, the provider says how it is
/// put together, and the engagement is kept on its own and driven through the same provider commands as a Contact Center
/// one.
/// </summary>
public sealed class PhoneCallSupervisionServiceTests
{
    private const string CallId = "agent-leg";
    private const string Key = "phone:agent-user:agent-leg";

    [Fact]
    public async Task TheDashboard_FindsTheCaller_AndTheColleagueAnExtensionCallRang()
    {
        // Arrange
        var fixture = new Fixture();
        fixture.ActiveCalls.Add(Call("keypad-leg", "caller-user", CallDirection.Outbound, to: "+17025550100"));
        fixture.ActiveCalls.Add(Call("ext-leg", "agent-user", CallDirection.Outbound, to: "Test 2", isExtension: true, extension: "2", userName: "Mike"));
        fixture.Extensions.Setup(value => value.ResolveAsync("2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtensionResolution { Found = true, Number = "2", UserId = "colleague-user" });

        // Act
        var calls = await fixture.Service.FindCallsAsync(["caller-user", "agent-user", "colleague-user", "idle-user"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, calls.Count);
        Assert.Equal("+17025550100", calls["caller-user"].Party);
        Assert.False(calls["caller-user"].IsCallee);

        var callee = calls["colleague-user"];
        Assert.Equal("ext-leg", callee.CallId);
        Assert.True(callee.IsCallee);
        Assert.True(callee.IsExtension);
        Assert.Equal(CallDirection.Inbound, callee.Direction);
        Assert.Equal("Mike", callee.Party);
        Assert.Equal("phone:colleague-user:ext-leg", callee.Key);
        Assert.False(calls.ContainsKey("idle-user"));
    }

    [Fact]
    public async Task Engage_TellsTheSupervisorsPhoneFirst_ThenRingsIt_OnTheCallsOwnShape()
    {
        // Arrange
        var fixture = new Fixture().WithKeypadCall();
        var order = new List<string>();
        fixture.Notifier = new SupervisorEngagementTests.RecordingNotifier(order);
        ContactCenterVoiceMonitoringRequest rung = null;
        fixture.Monitoring
            .Setup(value => value.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterVoiceMonitoringRequest, CancellationToken>((request, _) =>
            {
                order.Add("ring");
                rung = request;
            })
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderLegId = "supervisor-leg" });

        // Act
        var result = await fixture.Service.EngageAsync(Key, "sup-user", Fixture.Principal, MonitorMode.Whisper, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(["notify:Requested", "ring"], order);

        var requested = Assert.Single(fixture.Notifier.Engagements);
        Assert.Equal(Key, requested.InteractionId);
        Assert.Equal("agent-1", requested.AgentId);
        Assert.Equal("Ann Agent", requested.AgentName);
        Assert.Equal(requested.MonitorToken, rung.MonitorToken);

        Assert.Equal("number-leg", rung.ProviderCallId);
        Assert.Equal(CallId, rung.AgentLegId);
        Assert.Equal(MonitorMode.Whisper, rung.Mode);
        Assert.Equal("cc-sv-number-leg", rung.Metadata[ContactCenterPhoneCallMonitoringTarget.ConferenceMetadataKey]);

        var engagement = await fixture.Service.FindEngagementAsync(Key, "sup-user", TestContext.Current.CancellationToken);
        Assert.Equal("supervisor-leg", engagement.SupervisorLegId);
        Assert.Equal(MonitorMode.Whisper, engagement.Mode);
        Assert.Null(engagement.ConnectedUtc);
    }

    [Fact]
    public async Task Engage_IsRefused_OnTheSupervisorsOwnCall_ACallThatEnded_OrOneTheProviderCannotJoin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new Fixture().WithKeypadCall();

        Assert.False((await fixture.Service.EngageAsync(Key, "agent-user", Fixture.Principal, MonitorMode.Monitor, cancellationToken)).Succeeded);
        Assert.False((await fixture.Service.EngageAsync("phone:agent-user:another-leg", "sup-user", Fixture.Principal, MonitorMode.Monitor, cancellationToken)).Succeeded);
        Assert.False((await fixture.Service.EngageAsync("not-a-phone-call", "sup-user", Fixture.Principal, MonitorMode.Monitor, cancellationToken)).Succeeded);

        fixture.PhoneCalls
            .Setup(value => value.ResolvePhoneCallAsync(CallId, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactCenterPhoneCallMonitoringTarget)null);
        var unjoinable = await fixture.Service.EngageAsync(Key, "sup-user", Fixture.Principal, MonitorMode.Monitor, cancellationToken);

        Assert.False(unjoinable.Succeeded);
        fixture.Monitoring.Verify(value => value.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(await fixture.Service.FindEngagementAsync(Key, "sup-user", cancellationToken));
    }

    [Fact]
    public async Task Engage_WhenTheProviderRefuses_ForgetsTheEngagement_AndTellsThePhoneItEnded()
    {
        // Arrange
        var fixture = new Fixture().WithKeypadCall();
        fixture.Monitoring
            .Setup(value => value.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = false, ErrorMessage = "Open your soft phone first." });

        // Act
        var result = await fixture.Service.EngageAsync(Key, "sup-user", Fixture.Principal, MonitorMode.Monitor, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Open your soft phone first.", result.Reason);
        Assert.Equal(["Requested", "Ended"], fixture.Notifier.Engagements.Select(notification => notification.State));
        Assert.Null(await fixture.Service.FindEngagementAsync(Key, "sup-user", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSupervisorsLeg_Answering_ConnectsTheEngagement_AndHangingUp_EndsIt()
    {
        // Arrange
        var fixture = await new Fixture().WithKeypadCall().EngagedAsync(MonitorMode.Monitor);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act & Assert - a leg nobody knows is somebody else's (a Contact Center call's).
        Assert.False(await fixture.Service.OnAnsweredAsync("Telnyx", "number-leg", "somebody-elses-leg", cancellationToken));

        Assert.True(await fixture.Service.OnAnsweredAsync("Telnyx", "number-leg", "supervisor-leg", cancellationToken));
        Assert.NotNull((await fixture.Service.FindEngagementAsync(Key, "sup-user", cancellationToken)).ConnectedUtc);
        Assert.Equal("Connected", fixture.Notifier.Engagements.Last().State);

        Assert.True(await fixture.Service.OnEndedAsync("Telnyx", "number-leg", "supervisor-leg", null, null, cancellationToken));
        Assert.Null(await fixture.Service.FindEngagementAsync(Key, "sup-user", cancellationToken));
        Assert.Equal(("Ended", "supervisor-left"), (fixture.Notifier.Engagements.Last().State, fixture.Notifier.Engagements.Last().Reason));
        fixture.PhoneCalls.Verify(value => value.HangupPhoneCallLegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchMode_ChangesTheModeOnTheSameLeg_InTheCallsOwnConference()
    {
        // Arrange
        var fixture = await new Fixture().WithExtensionCall().EngagedAsync(MonitorMode.Monitor, key: "phone:colleague-user:agent-leg");
        ContactCenterVoiceMonitoringRequest switched = null;
        fixture.Interventions
            .Setup(value => value.SwitchModeAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterVoiceMonitoringRequest, CancellationToken>((request, _) => switched = request)
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });

        // Act
        var result = await fixture.Service.SwitchModeAsync("phone:colleague-user:agent-leg", "sup-user", Fixture.Principal, MonitorMode.Barge, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("supervisor-leg", switched.SupervisorLegId);
        Assert.Equal("colleague-leg", switched.AgentLegId);
        Assert.Equal("ext-agent-leg", switched.Metadata[ContactCenterPhoneCallMonitoringTarget.ConferenceMetadataKey]);
        Assert.Equal(MonitorMode.Barge, (await fixture.Service.FindEngagementAsync("phone:colleague-user:agent-leg", "sup-user", TestContext.Current.CancellationToken)).Mode);
        Assert.Equal("ModeChanged", fixture.Notifier.Engagements.Last().State);
    }

    [Fact]
    public async Task Stop_HangsUpTheSupervisorsLeg_ForgetsTheEngagement_AndTellsThePhone()
    {
        // Arrange
        var fixture = await new Fixture().WithKeypadCall().EngagedAsync(MonitorMode.Barge);
        ContactCenterVoiceMonitoringRequest stopped = null;
        fixture.Monitoring
            .Setup(value => value.StopAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterVoiceMonitoringRequest, CancellationToken>((request, _) => stopped = request)
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });

        // Act
        var result = await fixture.Service.StopAsync(Key, "sup-user", Fixture.Principal, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("supervisor-leg", stopped.SupervisorLegId);
        Assert.Equal("number-leg", stopped.ProviderCallId);
        Assert.Equal(CallId, stopped.AgentLegId);
        Assert.Null(await fixture.Service.FindEngagementAsync(Key, "sup-user", TestContext.Current.CancellationToken));
        Assert.Equal("Ended", fixture.Notifier.Engagements.Last().State);
    }

    [Fact]
    public async Task TakeOver_NeedsTheInterventionPermission_AConnectedLeg_AndACallThatCanBeTakenOver()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var notConnected = await new Fixture().WithKeypadCall().EngagedAsync(MonitorMode.Barge, connected: false);
        Assert.False((await notConnected.Service.TakeOverAsync(Key, "sup-user", Fixture.Principal, cancellationToken)).Succeeded);

        var extension = await new Fixture().WithExtensionCall().EngagedAsync(MonitorMode.Barge, key: "phone:colleague-user:agent-leg");
        Assert.False((await extension.Service.TakeOverAsync("phone:colleague-user:agent-leg", "sup-user", Fixture.Principal, cancellationToken)).Succeeded);

        var forbidden = await new Fixture(grantIntervene: false).WithKeypadCall().EngagedAsync(MonitorMode.Barge);
        Assert.False((await forbidden.Service.TakeOverAsync(Key, "sup-user", Fixture.Principal, cancellationToken)).Succeeded);

        foreach (var fixture in new[] { notConnected, extension, forbidden })
        {
            fixture.PhoneCalls.Verify(value => value.TakeOverPhoneCallAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task TakeOver_MakesTheCallTheSupervisors_SoTheirHangingUpEndsItForTheOtherParty()
    {
        // Arrange
        var fixture = await new Fixture().WithKeypadCall().EngagedAsync(MonitorMode.Monitor);
        var cancellationToken = TestContext.Current.CancellationToken;
        fixture.PhoneCalls
            .Setup(value => value.TakeOverPhoneCallAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });

        // Act
        var result = await fixture.Service.TakeOverAsync(Key, "sup-user", Fixture.Principal, cancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        fixture.PhoneCalls.Verify(value => value.TakeOverPhoneCallAsync(
            It.Is<ContactCenterVoiceMonitoringRequest>(request =>
                request.SupervisorLegId == "supervisor-leg" &&
                request.AgentLegId == CallId &&
                request.ProviderCallId == "number-leg" &&
                request.Mode == MonitorMode.Barge),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("TookOver", fixture.Notifier.Engagements.Last().State);

        // The agent's leg ending with the takeover does not let the supervisor go.
        await fixture.Service.ReleaseCallAsync(CallId, cancellationToken);
        fixture.Interventions.Verify(value => value.ReleaseSupervisorLegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True((await fixture.Service.FindEngagementAsync(Key, "sup-user", cancellationToken)).TookOver);

        // The supervisor hanging up ends it for the number too.
        Assert.True(await fixture.Service.OnEndedAsync("Telnyx", "number-leg", "supervisor-leg", null, null, cancellationToken));
        fixture.PhoneCalls.Verify(value => value.HangupPhoneCallLegAsync("number-leg", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await fixture.Service.FindEngagementAsync(Key, "sup-user", cancellationToken));
    }

    // Live, the agent's leg hanging up came back from the provider while the takeover was still being asked for, and the
    // call's end let go of the very supervisor who was taking it: the number was left alone on the line.
    [Fact]
    public async Task TakeOver_TheAgentsLegEndingWhileTheProviderIsStillHandingTheCallOver_DoesNotLetTheSupervisorGo()
    {
        // Arrange
        var fixture = await new Fixture().WithKeypadCall().EngagedAsync(MonitorMode.Barge);
        var cancellationToken = TestContext.Current.CancellationToken;
        fixture.PhoneCalls
            .Setup(value => value.TakeOverPhoneCallAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await fixture.Service.ReleaseCallAsync(CallId, cancellationToken);

                return new ContactCenterVoiceProviderResult { Succeeded = true };
            });

        // Act
        var result = await fixture.Service.TakeOverAsync(Key, "sup-user", Fixture.Principal, cancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Reason);
        fixture.Interventions.Verify(value => value.ReleaseSupervisorLegAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True((await fixture.Service.FindEngagementAsync(Key, "sup-user", cancellationToken)).TookOver);
        Assert.DoesNotContain(fixture.Notifier.Engagements, engagement => engagement.State == "Ended");
    }

    [Fact]
    public async Task TakeOver_TheProviderRefusing_LeavesTheSupervisorListeningAsBefore()
    {
        // Arrange
        var fixture = await new Fixture().WithKeypadCall().EngagedAsync(MonitorMode.Whisper);
        var cancellationToken = TestContext.Current.CancellationToken;
        fixture.PhoneCalls
            .Setup(value => value.TakeOverPhoneCallAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = false, ErrorMessage = "refused" });

        // Act
        var result = await fixture.Service.TakeOverAsync(Key, "sup-user", Fixture.Principal, cancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        var engagement = await fixture.Service.FindEngagementAsync(Key, "sup-user", cancellationToken);
        Assert.False(engagement.TookOver);
        Assert.Equal(MonitorMode.Whisper, engagement.Mode);

        // Not taken over, so the call ending still lets them go.
        await fixture.Service.ReleaseCallAsync(CallId, cancellationToken);
        fixture.Interventions.Verify(value => value.ReleaseSupervisorLegAsync("supervisor-leg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenTheCallEnds_EverySupervisorStillOnItIsLetGo()
    {
        // Arrange
        var fixture = await new Fixture().WithKeypadCall().EngagedAsync(MonitorMode.Monitor);

        // Act
        await fixture.Service.ReleaseCallAsync(CallId, TestContext.Current.CancellationToken);

        // Assert
        fixture.Interventions.Verify(value => value.ReleaseSupervisorLegAsync("supervisor-leg", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await fixture.Service.FindEngagementAsync(Key, "sup-user", TestContext.Current.CancellationToken));
        Assert.Equal(("Ended", "call-ended"), (fixture.Notifier.Engagements.Last().State, fixture.Notifier.Engagements.Last().Reason));
    }

    [Fact]
    public async Task EndCall_HangsUpTheAgentsCall_OrTheOtherPartyOfACallTheSupervisorTookOver()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var fixture = new Fixture().WithKeypadCall();
        Assert.True((await fixture.Service.EndCallAsync(Key, "sup-user", Fixture.Principal, cancellationToken)).Succeeded);
        fixture.Telephony.Verify(value => value.HangupAsync(It.Is<CallReference>(call => call.CallId == CallId), It.IsAny<CancellationToken>()), Times.Once);

        var takenOver = await new Fixture().WithKeypadCall().EngagedAsync(MonitorMode.Barge);
        takenOver.PhoneCalls
            .Setup(value => value.TakeOverPhoneCallAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
        await takenOver.Service.TakeOverAsync(Key, "sup-user", Fixture.Principal, cancellationToken);
        Assert.True((await takenOver.Service.EndCallAsync(Key, "sup-user", Fixture.Principal, cancellationToken)).Succeeded);
        takenOver.Telephony.Verify(value => value.HangupAsync(It.Is<CallReference>(call => call.CallId == "number-leg"), It.IsAny<CancellationToken>()), Times.Once);

        var forbidden = new Fixture(grantIntervene: false).WithKeypadCall();
        Assert.False((await forbidden.Service.EndCallAsync(Key, "sup-user", Fixture.Principal, cancellationToken)).Succeeded);
        forbidden.Telephony.Verify(value => value.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASupervisorMayActOnAnAgent_WhoWorksAQueueTheyOversee()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new Fixture();

        Assert.True(await fixture.Service.IsAuthorizedAsync(Fixture.Principal, "sup-user", Key, cancellationToken));
        Assert.False(await fixture.Service.IsAuthorizedAsync(Fixture.Principal, "other-supervisor", Key, cancellationToken));
        Assert.False(await fixture.Service.IsAuthorizedAsync(Fixture.Principal, "sup-user", "phone:unknown-user:leg", cancellationToken));
        Assert.False(await fixture.Service.IsAuthorizedAsync(Fixture.Principal, "sup-user", "interaction-1", cancellationToken));
    }

    [Fact]
    public void TheModesOffered_AreTheProvidersOnACallItCanJoin()
    {
        var fixture = new Fixture();
        var keypad = new AgentPhoneCall { UserId = "agent-user", CallId = CallId, ProviderName = "Telnyx", Direction = CallDirection.Outbound };

        Assert.Equal([MonitorMode.Monitor, MonitorMode.Whisper, MonitorMode.Barge], fixture.Service.GetAvailableModes(keypad));

        fixture.PhoneCalls.Setup(value => value.CanMonitorPhoneCall(false, false)).Returns(false);
        Assert.Empty(fixture.Service.GetAvailableModes(new AgentPhoneCall { UserId = "agent-user", CallId = CallId, ProviderName = "Telnyx", Direction = CallDirection.Inbound }));
        Assert.Empty(fixture.Service.GetAvailableModes(new AgentPhoneCall { UserId = "agent-user", CallId = CallId, ProviderName = "Unknown", Direction = CallDirection.Outbound }));
    }

    [Theory]
    [InlineData("phone:user-1:v3:abc:def", true, "user-1", "v3:abc:def")]
    [InlineData("phone:user-1:", false, null, null)]
    [InlineData("phone::leg", false, null, null)]
    [InlineData("interaction-1", false, null, null)]
    [InlineData(null, false, null, null)]
    public void APhoneCallKey_KeepsTheCallIdentifierWhole(string key, bool parsed, string userId, string callId)
    {
        Assert.Equal(parsed, PhoneCallKey.TryParse(key, out var readUser, out var readCall));
        Assert.Equal(userId, readUser);
        Assert.Equal(callId, readCall);

        if (parsed)
        {
            Assert.Equal(key, PhoneCallKey.Create(readUser, readCall));
        }
    }

    private static TelephonyInteraction Call(
        string callId,
        string userId,
        CallDirection direction,
        string to = null,
        bool isExtension = false,
        string extension = null,
        string userName = null)
        => new()
        {
            CallId = callId,
            UserId = userId,
            UserName = userName,
            ProviderName = "Telnyx",
            Direction = direction,
            From = "+17787200000",
            To = to,
            IsExtension = isExtension,
            ExtensionNumber = extension,
            StartedUtc = new DateTime(2026, 9, 25, 18, 5, 0, DateTimeKind.Utc),
        };

    private sealed class Fixture
    {
        public static readonly ClaimsPrincipal Principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "sup-user")], "test"));

        private readonly bool _grantIntervene;

        public Fixture(bool grantIntervene = true)
        {
            _grantIntervene = grantIntervene;

            var provider = new Mock<IContactCenterVoiceProvider>();
            provider.SetupGet(value => value.Capabilities).Returns(
                ContactCenterVoiceProviderCapabilities.Monitor |
                ContactCenterVoiceProviderCapabilities.Whisper |
                ContactCenterVoiceProviderCapabilities.Barge);
            Monitoring = provider.As<IContactCenterVoiceMonitoringProvider>();
            Interventions = provider.As<IContactCenterVoiceSupervisorInterventionProvider>();
            PhoneCalls = provider.As<IContactCenterVoicePhoneCallMonitoringProvider>();
            PhoneCalls.Setup(value => value.CanMonitorPhoneCall(It.IsAny<bool>(), It.IsAny<bool>()))
                .Returns<bool, bool>((isExtension, isOutbound) => isExtension || isOutbound);
            Resolver.Setup(value => value.Get("Telnyx")).Returns(provider.Object);

            Interactions.Setup(value => value.GetActiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => ActiveCalls);
            Agents.Setup(value => value.FindByUserIdAsync("agent-user", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "agent-user", DisplayName = "Ann Agent", QueueIds = ["queue-1"] });
            Agents.Setup(value => value.FindByUserIdAsync("colleague-user", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "agent-2", UserId = "colleague-user", DisplayName = "Cole League", QueueIds = ["queue-1"] });
            QueueAuthorization.Setup(value => value.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), "sup-user", "queue-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Telephony.Setup(value => value.HangupAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TelephonyResult { Succeeded = true });
        }

        public List<TelephonyInteraction> ActiveCalls { get; } = [];

        public Mock<ITelephonyInteractionStore> Interactions { get; } = new();

        public Mock<ITelephonyExtensionResolver> Extensions { get; } = new();

        public Mock<IContactCenterVoiceProviderResolver> Resolver { get; } = new();

        public Mock<IContactCenterVoiceMonitoringProvider> Monitoring { get; }

        public Mock<IContactCenterVoiceSupervisorInterventionProvider> Interventions { get; }

        public Mock<IContactCenterVoicePhoneCallMonitoringProvider> PhoneCalls { get; }

        public Mock<IAgentProfileManager> Agents { get; } = new();

        public Mock<ISupervisorQueueAuthorizationService> QueueAuthorization { get; } = new();

        public Mock<ITelephonyService> Telephony { get; } = new();

        public SupervisorEngagementTests.RecordingNotifier Notifier { get; set; } = new();

        private ContactCenterPhoneCallSupervisionService _service;

        private readonly IPhoneCallEngagementStore _store = new DistributedCachePhoneCallEngagementStore(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new ShellSettings { Name = "Default" });

        public ContactCenterPhoneCallSupervisionService Service
            => _service ??= new ContactCenterPhoneCallSupervisionService(
                _store,
                [Interactions.Object],
                [Extensions.Object],
                Resolver.Object,
                Agents.Object,
                QueueAuthorization.Object,
                new PermissionAuthorizationService(_grantIntervene),
                new DefaultTelephonyCommandExecutor(Options.Create(new TelephonyCommandOptions()), Mock.Of<IHostApplicationLifetime>()),
                Telephony.Object,
                [Notifier],
                new StubClock(),
                NullLogger<ContactCenterPhoneCallSupervisionService>.Instance);

        public Fixture WithKeypadCall()
        {
            ActiveCalls.Add(Call(CallId, "agent-user", CallDirection.Outbound, to: "+17025550100"));
            PhoneCalls.Setup(value => value.ResolvePhoneCallAsync(CallId, false, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactCenterPhoneCallMonitoringTarget
                {
                    ProviderCallId = "number-leg",
                    AgentLegId = CallId,
                    OtherPartyLegId = "number-leg",
                    ConferenceName = "cc-sv-number-leg",
                    CanTakeOver = true,
                });

            return this;
        }

        public Fixture WithExtensionCall()
        {
            ActiveCalls.Add(Call(CallId, "agent-user", CallDirection.Outbound, to: "Test 2", isExtension: true, extension: "2", userName: "Mike"));
            Extensions.Setup(value => value.ResolveAsync("2", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExtensionResolution { Found = true, Number = "2", UserId = "colleague-user" });
            PhoneCalls.Setup(value => value.ResolvePhoneCallAsync(CallId, true, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactCenterPhoneCallMonitoringTarget
                {
                    ProviderCallId = CallId,
                    AgentLegId = "colleague-leg",
                    OtherPartyLegId = CallId,
                    ConferenceName = "ext-agent-leg",
                });

            return this;
        }

        public async Task<Fixture> EngagedAsync(MonitorMode mode, string key = Key, bool connected = true)
        {
            Monitoring
                .Setup(value => value.EngageAsync(It.IsAny<ContactCenterVoiceMonitoringRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderLegId = "supervisor-leg" });

            var engaged = await Service.EngageAsync(key, "sup-user", Principal, mode, TestContext.Current.CancellationToken);
            Assert.True(engaged.Succeeded, engaged.Reason);

            if (connected)
            {
                Assert.True(await Service.OnAnsweredAsync("Telnyx", "any", "supervisor-leg", TestContext.Current.CancellationToken));
            }

            return this;
        }
    }

    private sealed class PermissionAuthorizationService : IAuthorizationService
    {
        private readonly bool _grantIntervene;

        public PermissionAuthorizationService(bool grantIntervene)
        {
            _grantIntervene = grantIntervene;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var granted = requirements.OfType<PermissionRequirement>().All(requirement =>
                requirement.Permission.Name == ContactCenterPermissions.MonitorContactCenter.Name ||
                (_grantIntervene && requirement.Permission.Name == ContactCenterPermissions.InterveneInCalls.Name));

            return Task.FromResult(granted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
