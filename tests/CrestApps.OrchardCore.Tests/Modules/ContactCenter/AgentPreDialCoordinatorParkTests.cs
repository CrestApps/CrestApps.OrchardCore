#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The agent leg parked for a call the dialer placed for a reserved agent: rung while the customer rings, answered by
/// the agent's phone on arrival, joined to the customer once a person answers, and released when nobody does.
/// </summary>
public sealed class AgentPreDialCoordinatorParkTests
{
    private static readonly DateTime _now = new(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ParkForDialedCallAsync_ForACallPlacedForAReservedAgent_RingsALegTheAgentsPhoneAnswersOnArrival()
    {
        // Arrange
        var harness = new Harness();
        ContactCenterAgentPreDialRequest? request = null;
        harness.PreDialProvider
            .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterAgentPreDialRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(Dialed("leg-1"));

        // Act
        var parked = await harness.Coordinator.ParkForDialedCallAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(parked);
        Assert.NotNull(request);
        Assert.True(request!.AnswerOnArrival);
        Assert.Equal("r1", request.ReservationId);
        Assert.Equal("call-1", request.ProviderCallId);
        Assert.Equal("u1", request.AgentUserId);

        // The phone answers on arrival; the ring window only bounds one that never does.
        Assert.Equal(30, request.TimeoutSeconds);

        var leg = await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken);
        Assert.NotNull(leg);
        Assert.True(leg!.ParkedForDialedCall);
        Assert.Equal("leg-1", leg.AgentLegId);
        Assert.Equal(_now.AddMinutes(10), leg.RingsUntilUtc);
    }

    [Fact]
    public async Task HumanAnswer_JoinsTheParkedLegTheAgentsPhoneAlreadyAnswered()
    {
        // Arrange
        var harness = await Harness.WithParkedLegAsync();

        // The agent's phone answered the parked leg while the customer rang. Nothing is joined yet.
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);
        harness.PreDialProvider.Verify(
            provider => provider.BridgePreDialedAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Act: a person answered; the connect takes the parked leg and readies the customer.
        var taken = await harness.Coordinator.TakeParkedLegAsync("r1", TestContext.Current.CancellationToken);
        var bridged = await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("leg-1", taken?.AgentLegId);
        Assert.True(bridged);
        harness.PreDialProvider.Verify(provider => provider.BridgePreDialedAgentAsync("call-1", "leg-1", It.IsAny<CancellationToken>()), Times.Once);
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Live: the agent talked to the customer while shown "Waiting for a connected call", because this join was
        // recorded only by the answer command's projection, which a command past its timeout never reached.
        harness.AgentLegFailureService.Verify(
            service => service.RecordAnsweredAsync("Telnyx", "call-1", "leg-1", CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task HumanAnswer_BeforeTheAgentsPhoneAnswered_JoinsTheLegWhenItIsAnswered()
    {
        // Arrange
        var harness = await Harness.WithParkedLegAsync();

        // Act: the customer answers within a moment of the dial, before the agent's phone picked the leg up.
        var taken = await harness.Coordinator.TakeParkedLegAsync("r1", TestContext.Current.CancellationToken);
        var bridgedOnAnswer = await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(taken);
        Assert.False(bridgedOnAnswer);
        harness.PreDialProvider.Verify(provider => provider.BridgePreDialedAgentAsync("call-1", "leg-1", It.IsAny<CancellationToken>()), Times.Once);
        harness.AgentLegFailureService.Verify(
            service => service.RecordAnsweredAsync("Telnyx", "call-1", "leg-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MachineAnswer_ReleasesTheParkedLegWithoutJoiningIt()
    {
        // Arrange
        var harness = await Harness.WithParkedLegAsync();
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);

        // Act: a machine answered; the verdict releases the parked leg.
        await harness.Coordinator.ReleaseAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
        harness.PreDialProvider.Verify(
            provider => provider.BridgePreDialedAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Null(await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken));

        // The leg's own end, coming back afterwards, fails nothing.
        await harness.Coordinator.OnAgentLegEndedAsync("Telnyx", "r1", "leg-1", HangupCause.NormalClearing, TestContext.Current.CancellationToken);
        harness.AgentLegFailureService.Verify(
            service => service.FailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NoAnswer_ReleasesTheParkedLegWhenTheCallEnds()
    {
        // Arrange
        var harness = await Harness.WithParkedLegAsync();
        await harness.Coordinator.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", TestContext.Current.CancellationToken);

        // Act: the customer's phone rang out; the call's end releases what was rung for it.
        await harness.Coordinator.ReleaseForInteractionAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CustomerHangsUpWhileTheLegIsBeingParked_HangsTheLegUp()
    {
        // Arrange
        var harness = new Harness();
        harness.PreDialProvider
            .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => harness.Interaction.RestorePersistedStatus(InteractionStatus.Ended))
            .ReturnsAsync(Dialed("leg-1"));

        // Act
        var parked = await harness.Coordinator.ParkForDialedCallAsync("int1", TestContext.Current.CancellationToken);

        // Assert: nothing would ever join or release the leg, so it is not left up.
        Assert.False(parked);
        harness.PreDialProvider.Verify(provider => provider.HangupPreDialedAgentAsync("leg-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ParkedLegEndingBeforeTheCustomerAnswers_FailsNothing_AndTheAnswerRingsTheAgent()
    {
        // Arrange
        var harness = await Harness.WithParkedLegAsync();

        // Act: the parked leg is lost while the customer still rings.
        await harness.Coordinator.OnAgentLegEndedAsync("Telnyx", "r1", "leg-1", HangupCause.Failed, TestContext.Current.CancellationToken);
        var taken = await harness.Coordinator.TakeParkedLegAsync("r1", TestContext.Current.CancellationToken);

        // Assert: only a head start was lost; the customer's call goes on, and the answer rings the agent.
        Assert.Null(taken);
        harness.AgentLegFailureService.Verify(
            service => service.FailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ParkedLegEndingAfterTheCustomerAnswered_FailsTheConnect()
    {
        // Arrange
        var harness = await Harness.WithParkedLegAsync();
        await harness.Coordinator.TakeParkedLegAsync("r1", TestContext.Current.CancellationToken);
        await harness.Coordinator.OnCallerReadyAsync("r1", TestContext.Current.CancellationToken);

        // Act: the leg the customer was waiting to be joined to is gone.
        await harness.Coordinator.OnAgentLegEndedAsync("Telnyx", "r1", "leg-1", HangupCause.NoAnswer, TestContext.Current.CancellationToken);

        // Assert
        harness.AgentLegFailureService.Verify(
            service => service.FailAsync("Telnyx", "call-1", HangupCause.NoAnswer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnswerArrivingBeforeThePark_KeepsThePark_FromRingingTheAgentASecondLeg()
    {
        // Arrange
        var harness = new Harness();
        harness.PreDialProvider
            .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Dialed("leg-1"));

        // Act: the customer answered before the park ran; the answer rings the agent the ordinary way.
        var taken = await harness.Coordinator.TakeParkedLegAsync("r1", TestContext.Current.CancellationToken);
        var parked = await harness.Coordinator.ParkForDialedCallAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(taken);
        Assert.False(parked);
        harness.PreDialProvider.Verify(
            provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ParkForDialedCallAsync_ForAnOverDialedCall_RingsNothing()
    {
        // Arrange
        var harness = new Harness();
        DialerCallMetadata.MarkOverDialed(harness.Interaction);

        // Act
        var parked = await harness.Coordinator.ParkForDialedCallAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(parked);
        harness.PreDialProvider.Verify(
            provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ParkForDialedCallAsync_WhenTheReservationIsNotAccepted_RingsNothing()
    {
        // Arrange
        var harness = new Harness();
        harness.Reservation.RestorePersistedStatus(ReservationStatus.Canceled);

        // Act
        var parked = await harness.Coordinator.ParkForDialedCallAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(parked);
        harness.PreDialProvider.Verify(
            provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ParkForDialedCallAsync_WhenTurnedOff_RingsNothing()
    {
        // Arrange
        var harness = new Harness(options => options.DialerAgentParkingEnabled = false);

        // Act
        var parked = await harness.Coordinator.ParkForDialedCallAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(parked);
        harness.PreDialProvider.Verify(
            provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RedialAgentLegAsync_ForAParkedLeg_RingsAReplacementThePhoneAnswersOnArrival()
    {
        // Arrange
        var harness = await Harness.WithParkedLegAsync();
        ContactCenterAgentPreDialRequest? request = null;
        harness.PreDialProvider
            .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterAgentPreDialRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(Dialed("leg-2"));

        // Act: the agent's phone moved to another registration.
        var redialed = await harness.Coordinator.RedialAgentLegAsync("Telnyx", "r1", "leg-1", "sip:new@example", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(redialed);
        Assert.True(request!.AnswerOnArrival);
        Assert.Equal(30, request.TimeoutSeconds);
        Assert.Equal("leg-2", (await harness.Store.FindAsync("r1", TestContext.Current.CancellationToken))!.AgentLegId);
    }

    private static ContactCenterVoiceProviderResult Dialed(string legId)
        => new()
        {
            Succeeded = true,
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            ProviderLegId = legId,
            ProviderLegState = VoiceCallState.Dialing,
        };

    private sealed class Harness
    {
        public Harness(Action<ContactCenterCoordinationOptions>? configure = null)
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            Store = new DistributedCacheAgentPreDialLegStore(
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
                clock.Object,
                new ShellSettings { Name = "Default" });

            ReservationManager
                .Setup(manager => manager.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Reservation);

            Interaction = new Interaction
            {
                ItemId = "int1",
                ActivityItemId = "act1",
                AgentId = "a1",
                ProviderName = "Telnyx",
                ProviderInteractionId = "call-1",
                Direction = InteractionDirection.Outbound,
            }.RestorePersistedStatus(InteractionStatus.Ringing);
            DialerCallMetadata.StampDial(Interaction, new DialerProfile { ItemId = "profile-1", Mode = DialerMode.Power, MaxAttempts = 3 }, 1);
            DialerCallMetadata.StampReservation(Interaction, "r1");

            InteractionManager
                .Setup(manager => manager.FindByIdAsync("int1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Interaction);

            AgentManager
                .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = "u1" });

            Provider.SetupGet(provider => provider.TechnicalName).Returns("Telnyx");
            Provider.SetupGet(provider => provider.DeliveryModel).Returns(VoiceProviderDeliveryModel.ServerSideAcd);
            PreDialProvider = Provider.As<IContactCenterVoiceAgentPreDialProvider>();
            PreDialProvider
                .Setup(provider => provider.BridgePreDialedAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderName = "Telnyx" });
            VoiceProviderResolver.Setup(resolver => resolver.Get("Telnyx")).Returns(Provider.Object);

            var distributedLock = new Mock<IDistributedLock>();
            distributedLock
                .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync(() => (new Mock<ILocker>().Object, true));

            var options = new ContactCenterCoordinationOptions();
            configure?.Invoke(options);

            Coordinator = new AgentPreDialCoordinator(
                Store,
                VoiceProviderResolver.Object,
                InteractionManager.Object,
                AgentManager.Object,
                AgentLegFailureService.Object,
                new StubScopeExecutor(ReservationManager.Object, InteractionManager.Object, AgentLegFailureService.Object),
                distributedLock.Object,
                clock.Object,
                Options.Create(options),
                NullLogger<AgentPreDialCoordinator>.Instance);
        }

        // The dialer accepted the reservation when it placed the call.
        public ActivityReservation Reservation { get; } = new ActivityReservation
        {
            ItemId = "r1",
            AgentId = "a1",
            ActivityItemId = "act1",
            QueueId = "q1",
            ExpiresUtc = _now.AddSeconds(20),
        }.RestorePersistedStatus(ReservationStatus.Accepted);

        public Interaction Interaction { get; }

        public DistributedCacheAgentPreDialLegStore Store { get; }

        public Mock<IActivityReservationManager> ReservationManager { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public Mock<IContactCenterAgentLegFailureService> AgentLegFailureService { get; } = new();

        public Mock<IContactCenterVoiceProviderResolver> VoiceProviderResolver { get; } = new();

        public Mock<IContactCenterVoiceProvider> Provider { get; } = new();

        public Mock<IContactCenterVoiceAgentPreDialProvider> PreDialProvider { get; }

        public AgentPreDialCoordinator Coordinator { get; }

        public static async Task<Harness> WithParkedLegAsync()
        {
            var harness = new Harness();
            harness.PreDialProvider
                .Setup(provider => provider.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Dialed("leg-1"));

            Assert.True(await harness.Coordinator.ParkForDialedCallAsync("int1", TestContext.Current.CancellationToken));

            return harness;
        }
    }

    private sealed class StubScopeExecutor : IContactCenterScopeExecutor
    {
        private readonly object[] _contexts;

        public StubScopeExecutor(params object[] contexts)
        {
            _contexts = contexts;
        }

        public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => operation(_contexts.OfType<TContext>().First());

        public Task ExecuteAsync(Func<IServiceProvider, Task> operation)
            => throw new NotSupportedException();

        public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
            where TContext : notnull
            => false;

        public bool ScheduleAfterCommit(Func<Task> operation)
            => false;
    }
}
