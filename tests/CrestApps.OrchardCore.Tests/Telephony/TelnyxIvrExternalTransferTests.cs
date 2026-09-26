using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProviderVoiceEvent = CrestApps.OrchardCore.Telephony.Models.ProviderVoiceEvent;
using VoiceCallState = CrestApps.OrchardCore.Telephony.Models.VoiceCallState;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A caller who chooses an external destination on an entry point's phone menu, transferred on Telnyx. Only an
/// enabled entry in the tenant's approved catalog is reachable, the destination sees the line the caller dialled,
/// and the call is settled and audited once it has left.
/// </summary>
public sealed class TelnyxIvrExternalTransferTests
{
    [Fact]
    public async Task AnApprovedDestination_IsTransferredTo_PresentingTheLineTheCallerDialled()
    {
        // Arrange
        var harness = new TransferHarness();

        // Act
        var transferred = await harness.TransferAsync("dest-billing");

        // Assert
        Assert.True(transferred);
        var request = harness.Http.Requests.Single();
        Assert.EndsWith("/calls/v3%3Acaller-1/actions/transfer", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);

        using var body = JsonDocument.Parse(harness.Http.RequestBodies.Single());
        Assert.Equal("+17025551234", body.RootElement.GetProperty("to").GetString());
        Assert.Equal("+17025550199", body.RootElement.GetProperty("from").GetString());
    }

    [Fact]
    public async Task ATransferTelnyxTakes_IsNotSettledUntilTheDestinationAnswers()
    {
        // Arrange
        // Telnyx accepting the transfer only means it is ringing the destination, which can still be busy or not
        // answer; the caller is then still on the line. Settling the call here ended it for a caller nobody was
        // talking to.
        var harness = new TransferHarness();

        // Act
        await harness.TransferAsync("dest-billing");

        // Assert
        Assert.Empty(harness.Ingested);
        Assert.NotEqual(ActivityStatus.Completed, harness.Activity.Status);
        Assert.Equal("dest-billing", harness.Interaction.TechnicalMetadata[IvrExternalTransferService.PendingDestinationMetadataKey]);
        var audit = harness.Audit.Single();
        Assert.Equal(ContactCenterConstants.Events.IvrActionTaken, audit.EventType);
        Assert.Equal("ExternalTransferRinging", audit.Data.Reason);
    }

    [Fact]
    public async Task TheLegTheTransferRings_IsMarkedSoItsAnswerOrHangupComesBackToTheCaller()
    {
        // Arrange
        var harness = new TransferHarness();

        // Act
        await harness.TransferAsync("dest-billing");

        // Assert
        using var body = JsonDocument.Parse(harness.Http.RequestBodies.Single());
        var encoded = body.RootElement.GetProperty("target_leg_client_state").GetString();
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        Assert.True(TelnyxCallFlowClientState.TryParse(decoded, out var state));
        Assert.Equal(TelnyxCallFlowClientState.TransferLegIntent, state.Intent);
        Assert.Equal("interaction-1", state.InteractionId);
        Assert.False(body.RootElement.TryGetProperty("client_state", out _));
    }

    [Fact]
    public async Task ATransferTheDestinationAnswered_IsSettledAndAudited()
    {
        // Arrange
        var harness = new TransferHarness();
        await harness.TransferAsync("dest-billing");

        // Act
        var completed = await harness.Service.CompleteAsync(harness.Interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(completed);
        Assert.Equal(VoiceCallState.Transferred, harness.Ingested.Single().State);
        Assert.Equal(ActivityStatus.Completed, harness.Activity.Status);
        Assert.Equal(IvrExternalTransferService.ReasonCode, harness.Activity.TerminalReasonCode);
        var audit = harness.Audit.Last();
        Assert.Equal(ContactCenterConstants.Events.IvrActionTaken, audit.EventType);
        Assert.Equal("ExternalTransferCompleted", audit.Data.Reason);
        Assert.Equal("+17025551234", audit.Data.Target);
        Assert.False(harness.Interaction.TechnicalMetadata.ContainsKey(IvrExternalTransferService.PendingDestinationMetadataKey));
    }

    [Fact]
    public async Task ATransferTheDestinationNeverAnswered_IsAuditedWithTheCause_AndHandedBack()
    {
        // Arrange
        var harness = new TransferHarness();
        await harness.TransferAsync("dest-billing");

        // Act
        var failed = await harness.Service.FailAsync(harness.Interaction, "user_busy", callerLeft: false, TestContext.Current.CancellationToken);
        var completedAfterwards = await harness.Service.CompleteAsync(harness.Interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("dest-billing", failed);
        Assert.False(completedAfterwards);
        Assert.Empty(harness.Ingested);
        Assert.NotEqual(ActivityStatus.Completed, harness.Activity.Status);
        var audit = harness.Audit.Last();
        Assert.Equal(ContactCenterConstants.Events.IvrFallbackTaken, audit.EventType);
        Assert.Equal("ExternalTransferFailed", audit.Data.Reason);
        Assert.Equal("user_busy", audit.Data.Details["hangupCause"]);
        Assert.Equal("+17025551234", audit.Data.Target);
    }

    [Fact]
    public async Task AnOutcomeForATransferThatIsNotWaiting_ChangesNothing()
    {
        // Arrange
        var harness = new TransferHarness();

        // Act
        var failed = await harness.Service.FailAsync(harness.Interaction, "user_busy", callerLeft: false, TestContext.Current.CancellationToken);
        var completed = await harness.Service.CompleteAsync(harness.Interaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(failed);
        Assert.False(completed);
        Assert.Empty(harness.Audit);
        Assert.Empty(harness.Ingested);
    }

    [Theory]
    [InlineData("dest-disabled")]
    [InlineData("dest-unknown")]
    [InlineData("dest-premium")]
    [InlineData("dest-own-line")]
    [InlineData(null)]
    public async Task ADestinationThatIsNotApprovedAndDialable_IsNeverCalled(string destinationId)
    {
        // Arrange
        // The menu stores only a catalog id; a disabled entry, one the dial policy refuses, or one of the contact
        // center's own lines (which would ring straight back into the menu) is not reachable.
        var harness = new TransferHarness();

        // Act
        var transferred = await harness.TransferAsync(destinationId);

        // Assert
        Assert.False(transferred);
        Assert.Empty(harness.Http.Requests);
        Assert.Empty(harness.Ingested);
    }

    [Fact]
    public async Task ATransferTelnyxRefuses_LeavesTheCallerForTheMenuToPutSomewhereElse()
    {
        // Arrange
        var harness = new TransferHarness(HttpStatusCode.UnprocessableEntity);

        // Act
        var transferred = await harness.TransferAsync("dest-billing");

        // Assert
        Assert.False(transferred);
        Assert.Empty(harness.Ingested);
        Assert.NotEqual(ActivityStatus.Completed, harness.Activity.Status);
    }

    private sealed class TransferHarness
    {
        public TransferHarness(HttpStatusCode status = HttpStatusCode.OK)
        {
            Http = new StubHttpMessageHandler(status, "{\"data\":{}}");
            var provider = TelnyxContactCenterProviderFactory.Create(Http);

            var providers = new Mock<IContactCenterVoiceProviderResolver>();
            providers.Setup(x => x.Get(It.IsAny<string>())).Returns(provider);

            var settings = new ContactCenterExternalTransferSettings
            {
                Destinations =
                [
                    new ContactCenterExternalDestination { Id = "dest-billing", DisplayName = "Billing", E164Address = "+17025551234", Enabled = true },
                    new ContactCenterExternalDestination { Id = "dest-disabled", DisplayName = "Old", E164Address = "+17025554321", Enabled = false },
                    new ContactCenterExternalDestination { Id = "dest-premium", DisplayName = "Premium", E164Address = "+19005550100", Enabled = true },
                    new ContactCenterExternalDestination { Id = "dest-own-line", DisplayName = "Us", E164Address = "+17025550199", Enabled = true },
                ],
            };

            var ownNumbers = new Mock<IContactCenterOwnNumberSource>();
            ownNumbers.Setup(x => x.GetOwnNumbersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["+17025550199"]);

            var workStates = new Mock<IContactCenterWorkStateService>();

            var activities = new Mock<IContactCenterActivityWriter>();
            activities.Setup(x => x.ScheduleUpdateAsync(It.IsAny<string>(), It.IsAny<Action<OmnichannelActivity>>(), It.IsAny<CancellationToken>()))
                .Callback<string, Action<OmnichannelActivity>, CancellationToken>((_, mutate, _) => mutate(Activity))
                .Returns(Task.CompletedTask);

            var voiceEvents = new Mock<IProviderVoiceEventService>();
            voiceEvents.Setup(x => x.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderVoiceEvent, CancellationToken>((voiceEvent, _) => Ingested.Add(voiceEvent))
                .ReturnsAsync((CallSession)null);

            var audit = new Mock<IContactCenterAuditRecorder>();
            audit.Setup(x => x.RecordCallAsync(
                    It.IsAny<string>(),
                    It.IsAny<CallLifecycleEventData>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<ContactCenterActor>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, CallLifecycleEventData, DateTime, ContactCenterActor, string, CancellationToken>((eventType, data, _, _, _, _) => Audit.Add((eventType, data)))
                .Returns(Task.CompletedTask);

            Service = new IvrExternalTransferService(
                SiteServiceFactory.Create(settings),
                DialDestinationPolicyFactory.Create(),
                [ownNumbers.Object],
                providers.Object,
                new Mock<IInteractionManager>().Object,
                workStates.Object,
                activities.Object,
                voiceEvents.Object,
                audit.Object,
                new Mock<global::YesSql.ISession>().Object,
                new TestClock(),
                NullLogger<IvrExternalTransferService>.Instance);

            Interaction = new Interaction
            {
                ItemId = "interaction-1",
                ActivityItemId = "activity-1",
                ProviderName = "Telnyx",
                ProviderInteractionId = "v3:caller-1",
                Channel = InteractionChannel.Voice,
                Direction = InteractionDirection.Inbound,
            };
            Interaction.TechnicalMetadata[ContactCenterConstants.TelephonyMetadata.ServiceAddress] = "+17025550199";
        }

        public StubHttpMessageHandler Http { get; }

        public IvrExternalTransferService Service { get; }

        public Interaction Interaction { get; }

        public OmnichannelActivity Activity { get; } = new() { ItemId = "activity-1", Status = ActivityStatus.AwaitingAgentResponse };

        public List<ProviderVoiceEvent> Ingested { get; } = [];

        public List<(string EventType, CallLifecycleEventData Data)> Audit { get; } = [];

        public Task<bool> TransferAsync(string destinationId)
            => Service.TransferAsync(Interaction, destinationId, TestContext.Current.CancellationToken);
    }
}
