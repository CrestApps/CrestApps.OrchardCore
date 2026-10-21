using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A Power call rings its reserved agent while the customer is still ringing: the agent's phone answers that leg at once
/// and it waits, parked, so a person answering (or the verdict that a person answered) only has to join the two. A
/// machine, a call nobody answers, or a customer who hangs up first releases the parked leg, and the agent goes back to
/// ready with no wrap-up.
/// </summary>
public sealed class ParkedAgentLegIntegrationTests
{
    private const string AgentOne = "agent-1";
    private const string ParkedLegId = "parked-leg-1";

    [Fact]
    public async Task Power_PlacedCall_ParksTheAgentLegWhileTheCustomerRings()
    {
        // Arrange
        var parking = new ParkingProvider();
        await using var harness = await CreateHarnessAsync(parking);

        // Act
        await PlacePowerCallAsync(harness);

        // Assert: the agent was rung once, on a leg their phone answers on arrival, tagged with the accepted reservation.
        var reservation = Assert.Single(await harness.GetReservationsAsync());
        var request = Assert.Single(parking.PreDialRequests);
        Assert.True(request.AnswerOnArrival);
        Assert.Equal(reservation.ItemId, request.ReservationId);
        Assert.Equal("user-1", request.AgentUserId);

        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(interaction.ProviderInteractionId, request.ProviderCallId);

        var leg = await harness.Services.GetRequiredService<IAgentPreDialLegStore>().FindAsync(reservation.ItemId, TestContext.Current.CancellationToken);
        Assert.NotNull(leg);
        Assert.True(leg.ParkedForDialedCall);
        Assert.Equal(ParkedLegId, leg.AgentLegId);
    }

    [Fact]
    public async Task Power_CustomerAnswers_JoinsTheParkedLegInsteadOfRingingTheAgent()
    {
        // Arrange
        var parking = new ParkingProvider();
        await using var harness = await CreateHarnessAsync(parking);
        await PlacePowerCallAsync(harness);
        var reservation = Assert.Single(await harness.GetReservationsAsync());

        // The agent's phone answered the parked leg while the customer rang.
        await harness.Services.GetRequiredService<IAgentPreDialCoordinator>()
            .OnAgentLegAnsweredAsync(DialerModeIntegrationHarness.ProviderName, reservation.ItemId, ParkedLegId, TestContext.Current.CancellationToken);

        // Act
        harness.Clock.Advance(TimeSpan.FromSeconds(12));
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Assert: the connect names the parked leg, so it only joins it; and it asks nothing of the customer's leg.
        var answer = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Contains($"\"PreDialedAgentLegId\":\"{ParkedLegId}\"", answer.RequestPayload, StringComparison.Ordinal);
        Assert.Contains("\"CallerAnswered\":true", answer.RequestPayload, StringComparison.Ordinal);
        Assert.Single(parking.PreDialRequests);
        Assert.Empty(parking.HungUpLegs);
    }

    [Fact]
    public async Task Power_ScreenedCall_HoldsTheParkedLegUntilAPersonIsHeard_ThenJoinsIt()
    {
        // Arrange
        var parking = new ParkingProvider();
        await using var harness = await CreateHarnessAsync(parking);
        await PlacePowerCallAsync(harness, screened: true);

        // Act: answered, and the provider still listening.
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Assert: nobody is connected before the verdict, and the parked leg stays up.
        Assert.DoesNotContain(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Empty(parking.HungUpLegs);

        // Act: a person answered.
        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        await harness.RaiseAnswerVerdictAsync("activity-1", AnswerClassification.Human);

        // Assert
        var answer = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Contains($"\"PreDialedAgentLegId\":\"{ParkedLegId}\"", answer.RequestPayload, StringComparison.Ordinal);
        Assert.Empty(parking.HungUpLegs);
    }

    [Fact]
    public async Task Power_MachineAnswers_ReleasesTheParkedLeg_AndTheAgentSkipsWrapUp()
    {
        // Arrange
        var parking = new ParkingProvider();
        await using var harness = await CreateHarnessAsync(parking);
        await PlacePowerCallAsync(harness, screened: true);
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Act
        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        await harness.RaiseAnswerVerdictAsync("activity-1", AnswerClassification.Machine);

        // Assert: the parked leg goes at once, and no agent is connected.
        Assert.Contains(ParkedLegId, parking.HungUpLegs);
        Assert.DoesNotContain(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);

        // Act: the hang-up comes back as the call's end.
        await harness.RaiseCallEndedAsync("activity-1");

        // Assert: the agent never joined the call and goes straight back to ready.
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.False(DialerCallMetadata.HasAgentJoined(interaction));
        Assert.Null(interaction.WrapUpStartedUtc);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
    }

    [Fact]
    public async Task Power_NobodyAnswers_ReleasesTheParkedLeg_AndTheAgentSkipsWrapUp()
    {
        // Arrange
        var parking = new ParkingProvider();
        await using var harness = await CreateHarnessAsync(parking);
        await PlacePowerCallAsync(harness);

        // Act: the customer's phone rings out.
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        await harness.RaiseCallStateAsync("activity-1", VoiceCallState.NoAnswer, "no-answer");
        await harness.ReleaseEndedCallAsync("activity-1");

        // Assert
        Assert.Contains(ParkedLegId, parking.HungUpLegs);
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.False(DialerCallMetadata.HasAgentJoined(interaction));
        Assert.Null(interaction.WrapUpStartedUtc);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
    }

    [Fact]
    public async Task Power_CustomerHangsUpWhileTheAgentIsParked_ReleasesTheParkedLeg()
    {
        // Arrange
        var parking = new ParkingProvider();
        await using var harness = await CreateHarnessAsync(parking);
        await PlacePowerCallAsync(harness);

        // Act: the customer declines the call before it is answered.
        harness.Clock.Advance(TimeSpan.FromSeconds(5));
        await harness.RaiseCallEndedAsync("activity-1");

        // Assert
        Assert.Contains(ParkedLegId, parking.HungUpLegs);
        Assert.DoesNotContain(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
    }

    [Fact]
    public async Task Power_ParkingTurnedOff_RingsTheAgentOnlyOnceTheCustomerAnswers()
    {
        // Arrange
        var parking = new ParkingProvider();
        await using var harness = await CreateHarnessAsync(parking, options => options.DialerAgentParkingEnabled = false);
        await PlacePowerCallAsync(harness);

        // Act
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Assert
        Assert.Empty(parking.PreDialRequests);
        var answer = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.DoesNotContain("\"PreDialedAgentLegId\":\"", answer.RequestPayload, StringComparison.Ordinal);
        Assert.Contains("\"CallerAnswered\":true", answer.RequestPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Power_ScreenedCall_WithoutParking_HoldsTheAgentOnTheAnswer_AndConnectsThemOnlyOnTheHumanVerdict()
    {
        // Arrange: a provider that rings the agent only once a person is heard.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await PlacePowerCallAsync(harness, screened: true);

        // Act: answered, and the provider still listening.
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Assert: the agent is held, not connected, and still Reserved for the call -- not on a call while it is screened.
        Assert.DoesNotContain(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Equal(AgentPresenceStatus.Reserved, await harness.GetPresenceAsync(AgentOne));

        // Act: a person answered.
        harness.Clock.Advance(TimeSpan.FromSeconds(2));
        await harness.RaiseAnswerVerdictAsync("activity-1", AnswerClassification.Human);

        // Assert: the agent is connected now, and the person counts as answered from the verdict.
        var answer = Assert.Single(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        Assert.Contains("\"AgentUserId\":\"user-1\"", answer.RequestPayload, StringComparison.Ordinal);
        Assert.Contains("\"CallerAnswered\":true", answer.RequestPayload, StringComparison.Ordinal);
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.Equal(harness.Clock.UtcNow, DialerCallMetadata.GetLiveAnsweredUtc(interaction));
    }

    [Fact]
    public async Task Power_ScreenedCall_WithoutParking_MachineVerdict_ReleasesTheAgentToAvailableWithoutWrapUp()
    {
        // Arrange
        await using var harness = await DialerModeIntegrationHarness.CreateAsync(predictive: true);
        await PlacePowerCallAsync(harness, screened: true);
        await harness.RaiseHumanAnswerAsync("activity-1");

        // Act
        harness.Clock.Advance(TimeSpan.FromSeconds(3));
        await harness.RaiseAnswerVerdictAsync("activity-1", AnswerClassification.Machine);
        await harness.RaiseCallEndedAsync("activity-1");

        // Assert
        Assert.DoesNotContain(harness.Shared.Commands.All, command => command.CommandType == ProviderCommandType.Answer);
        var interaction = await harness.FindInteractionByActivityAsync("activity-1");
        Assert.False(DialerCallMetadata.HasAgentJoined(interaction));
        Assert.Null(interaction.WrapUpStartedUtc);
        Assert.Equal(AgentPresenceStatus.Available, await harness.GetPresenceAsync(AgentOne));
    }

    private static async Task PlacePowerCallAsync(DialerModeIntegrationHarness harness, bool screened = false)
    {
        await harness.SignInAgentAsync(AgentOne, "user-1");
        await harness.SeedQueuedActivityAsync("activity-1", "+15551230001");
        var profile = DialerModeIntegrationHarness.CreateProfile(DialerMode.Power);

        if (screened)
        {
            profile.AnsweringMachineDetection = DialerAnsweringMachineDetection.Standard;
            harness.Router.ReportsAnswerDetection = true;
        }

        // Read by the abandonment tracker, which measures an automated profile's answer-to-agent time.
        harness.Shared.Profiles.Add(profile);

        Assert.Equal(1, await harness.RunPacingCycleAsync(profile));
    }

    private static Task<DialerModeIntegrationHarness> CreateHarnessAsync(ParkingProvider parking, Action<ContactCenterCoordinationOptions> configureOptions = null)
        => DialerModeIntegrationHarness.CreateAsync(predictive: true, configure: services =>
        {
            services.AddSingleton<IAgentPreDialLegStore>(sp => new DistributedCacheAgentPreDialLegStore(
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
                sp.GetRequiredService<IClock>(),
                new ShellSettings { Name = "Default" }));
            services.AddSingleton(parking.Resolver);
            services.Configure<ContactCenterCoordinationOptions>(options => configureOptions?.Invoke(options));
            services.AddSingleton<IAgentPreDialCoordinator, AgentPreDialCoordinator>();
            services.AddSingleton(sp => new Lazy<IAgentPreDialCoordinator>(sp.GetRequiredService<IAgentPreDialCoordinator>));
        });

    // A provider like Telnyx: the agent joins through a leg of their own, which it can ring ahead of the answer.
    private sealed class ParkingProvider
    {
        public ParkingProvider()
        {
            var provider = new Mock<IContactCenterVoiceProvider>();
            provider.As<IContactCenterVoiceCallControlProvider>();
            provider.SetupGet(value => value.TechnicalName).Returns(DialerModeIntegrationHarness.ProviderName);
            provider.SetupGet(value => value.DeliveryModel).Returns(VoiceProviderDeliveryModel.ServerSideAcd);
            provider.SetupGet(value => value.Capabilities).Returns(ContactCenterVoiceProviderCapabilities.AgentConnect | ContactCenterVoiceProviderCapabilities.DialerDial);

            var preDial = provider.As<IContactCenterVoiceAgentPreDialProvider>();
            preDial
                .Setup(value => value.PreDialAgentAsync(It.IsAny<ContactCenterAgentPreDialRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ContactCenterAgentPreDialRequest, CancellationToken>((request, _) => PreDialRequests.Add(request))
                .ReturnsAsync(new ContactCenterVoiceProviderResult
                {
                    Succeeded = true,
                    ProviderName = DialerModeIntegrationHarness.ProviderName,
                    ProviderLegId = ParkedLegId,
                    ProviderLegState = VoiceCallState.Dialing,
                });
            preDial
                .Setup(value => value.HangupPreDialedAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((legId, _) => HungUpLegs.Add(legId))
                .Returns(Task.CompletedTask);

            var resolver = new Mock<IContactCenterVoiceProviderResolver>();
            resolver.Setup(value => value.Get(It.IsAny<string>())).Returns(provider.Object);
            Resolver = resolver.Object;
        }

        public IContactCenterVoiceProviderResolver Resolver { get; }

        public List<ContactCenterAgentPreDialRequest> PreDialRequests { get; } = [];

        public List<string> HungUpLegs { get; } = [];
    }
}
