using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerProviderCommandDispatchValidatorTests
{
    [Fact]
    public async Task CanDispatchAsync_WhenCurrentEligibilityAllows_ReturnsTrue()
    {
        // Arrange
        var profile = new DialerProfile { ItemId = "profile-1" };
        var activity = new OmnichannelActivity { ItemId = "activity-1" };
        var profileManager = new Mock<IDialerProfileManager>();
        profileManager
            .Setup(value => value.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var eligibilityService = new Mock<IDialerEligibilityService>();
        eligibilityService
            .Setup(value => value.EvaluateAsync(
                It.Is<DialerEligibilityContext>(context =>
                    context.Profile == profile &&
                    context.Activity == activity &&
                    // The dispatch re-validation must exclude the already-counted in-flight attempt from the
                    // maximum-attempts gate, or a single-attempt profile would suppress its own only dial.
                    context.AttemptAlreadyCounted),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Eligible());
        var validator = new DialerProviderCommandDispatchValidator(
            profileManager.Object,
            activityManager.Object,
            eligibilityService.Object,
            new Mock<IActivityReservationManager>().Object,
            new Mock<IAgentProfileManager>().Object,
            new Mock<IInteractionManager>().Object,
            NullLogger<DialerProviderCommandDispatchValidator>.Instance);

        // Act
        var result = await validator.CanDispatchAsync(Command(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenPolicyNowSuppresses_ReturnsFalse()
    {
        // Arrange
        var profileManager = new Mock<IDialerProfileManager>();
        profileManager
            .Setup(value => value.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerProfile { ItemId = "profile-1" });
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "activity-1" });
        var eligibilityService = new Mock<IDialerEligibilityService>();
        eligibilityService
            .Setup(value => value.EvaluateAsync(
                It.IsAny<DialerEligibilityContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Suppressed(
                DialerSuppressionReason.DoNotCall,
                "The destination is no longer eligible."));
        var validator = new DialerProviderCommandDispatchValidator(
            profileManager.Object,
            activityManager.Object,
            eligibilityService.Object,
            new Mock<IActivityReservationManager>().Object,
            new Mock<IAgentProfileManager>().Object,
            new Mock<IInteractionManager>().Object,
            NullLogger<DialerProviderCommandDispatchValidator>.Instance);

        // Act
        var result = await validator.CanDispatchAsync(Command(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenProfileIsMissing_FailsClosed()
    {
        // Arrange
        var validator = new DialerProviderCommandDispatchValidator(
            new Mock<IDialerProfileManager>().Object,
            new Mock<IOmnichannelActivityManager>().Object,
            new Mock<IDialerEligibilityService>().Object,
            new Mock<IActivityReservationManager>().Object,
            new Mock<IAgentProfileManager>().Object,
            new Mock<IInteractionManager>().Object,
            NullLogger<DialerProviderCommandDispatchValidator>.Instance);

        // Act
        var result = await validator.CanDispatchAsync(Command(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    // The callback profile is built in, never stored: a dial made with it must not be refused as having lost its profile.
    [Fact]
    public async Task CanDispatchAsync_ForTheBuiltInCallbackProfile_ValidatesWithoutAStoredProfile()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", Source = ActivitySources.Callback };
        var profileManager = new Mock<IDialerProfileManager>(MockBehavior.Strict);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var eligibilityService = new Mock<IDialerEligibilityService>();
        eligibilityService
            .Setup(value => value.EvaluateAsync(
                It.Is<DialerEligibilityContext>(context => context.Profile.ItemId == QueueCallbackDialerProfile.Id && context.Activity == activity),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Eligible());
        var validator = new DialerProviderCommandDispatchValidator(
            profileManager.Object,
            activityManager.Object,
            eligibilityService.Object,
            new Mock<IActivityReservationManager>().Object,
            new Mock<IAgentProfileManager>().Object,
            new Mock<IInteractionManager>().Object,
            NullLogger<DialerProviderCommandDispatchValidator>.Instance);
        var command = Command();
        command.DialerProfileId = QueueCallbackDialerProfile.Id;

        // Act
        var result = await validator.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    // A Power dial deferred by a node shutting down was sent by the next node after the agent had been let go and the
    // dialer had reserved them for another customer: both customers rang for one agent.
    [Fact]
    public async Task CanDispatchAsync_WhenAutomatedDialStillHoldsTheAgent_ReturnsTrue()
    {
        // Arrange
        var held = HeldDial(DialerMode.Power);
        held.Agent.PresenceStatus = AgentPresenceStatus.Reserved;
        held.Agent.DialHoldReservationId = "reservation-1";

        // Act
        var result = await held.Validator.CanDispatchAsync(held.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenAutomatedDialAgentWasReleased_ReturnsFalse()
    {
        // Arrange: the session expired across a restart, the hold went with it, and the agent came back Available.
        var held = HeldDial(DialerMode.Power);
        held.Agent.PresenceStatus = AgentPresenceStatus.Available;
        held.Agent.DialHoldReservationId = null;

        // Act
        var result = await held.Validator.CanDispatchAsync(held.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        held.Eligibility.Verify(value => value.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenAutomatedDialAgentIsHeldForAnotherDial_ReturnsFalse()
    {
        // Arrange
        var held = HeldDial(DialerMode.Power);
        held.Agent.PresenceStatus = AgentPresenceStatus.Reserved;
        held.Agent.DialHoldReservationId = "reservation-2";

        // Act
        var result = await held.Validator.CanDispatchAsync(held.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenReservationWasCanceled_ReturnsFalse()
    {
        // Arrange
        var held = HeldDial(DialerMode.Power, ReservationStatus.Canceled);
        held.Agent.PresenceStatus = AgentPresenceStatus.Reserved;
        held.Agent.DialHoldReservationId = "reservation-1";

        // Act
        var result = await held.Validator.CanDispatchAsync(held.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenAgentIsAlreadyOnAnotherCall_ReturnsFalse()
    {
        // Arrange
        var held = HeldDial(DialerMode.Preview);
        held.Agent.PresenceStatus = AgentPresenceStatus.Busy;
        held.ActiveInteractions.Add(VoiceCall("interaction-2", InteractionStatus.Ringing, InteractionStatus.Connected));

        // Act
        var result = await held.Validator.CanDispatchAsync(held.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(AgentPresenceStatus.Busy, true)]
    [InlineData(AgentPresenceStatus.Available, false)]
    [InlineData(AgentPresenceStatus.Offline, false)]
    public async Task CanDispatchAsync_ForPreviewDial_RequiresTheAgentStillBusyOnIt(AgentPresenceStatus presence, bool expected)
    {
        // Arrange
        var held = HeldDial(DialerMode.Preview);
        held.Agent.PresenceStatus = presence;

        // The dial's own interaction names the agent too, and must not count as another call.
        held.ActiveInteractions.Add(VoiceCall("interaction-1", InteractionStatus.Ringing));

        // Act
        var result = await held.Validator.CanDispatchAsync(held.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result);
    }

    private static Interaction VoiceCall(string interactionId, params InteractionStatus[] statuses)
    {
        var interaction = new Interaction
        {
            ItemId = interactionId,
            AgentId = "agent-1",
            Channel = InteractionChannel.Voice,
        };

        foreach (var status in statuses)
        {
            interaction.TransitionTo(status);
        }

        return interaction;
    }

    private static HeldDialFixture HeldDial(DialerMode mode, ReservationStatus reservationStatus = ReservationStatus.Accepted)
    {
        var reservation = new ActivityReservation { ItemId = "reservation-1", AgentId = "agent-1" };
        reservation.TransitionTo(reservationStatus);

        var fixture = new HeldDialFixture
        {
            Agent = new AgentProfile { ItemId = "agent-1", UserId = "user-1" },
        };

        var profileManager = new Mock<IDialerProfileManager>();
        profileManager
            .Setup(value => value.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DialerProfile { ItemId = "profile-1", Mode = mode });
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(value => value.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "activity-1" });
        fixture.Eligibility
            .Setup(value => value.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Eligible());
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(value => value.FindByIdAsync("reservation-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(value => value.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fixture.Agent);
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(value => value.GetActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fixture.ActiveInteractions);

        fixture.Validator = new DialerProviderCommandDispatchValidator(
            profileManager.Object,
            activityManager.Object,
            fixture.Eligibility.Object,
            reservationManager.Object,
            agentManager.Object,
            interactionManager.Object,
            NullLogger<DialerProviderCommandDispatchValidator>.Instance);
        fixture.Command = Command();
        fixture.Command.ReservationId = "reservation-1";
        fixture.Command.InteractionId = "interaction-1";

        return fixture;
    }

    private sealed class HeldDialFixture
    {
        public AgentProfile Agent { get; set; }

        public List<Interaction> ActiveInteractions { get; } = [];

        public Mock<IDialerEligibilityService> Eligibility { get; } = new();

        public DialerProviderCommandDispatchValidator Validator { get; set; }

        public ProviderCommand Command { get; set; }
    }

    private static ProviderCommand Command()
    {
        return new ProviderCommand
        {
            CommandId = "command-1",
            DialerProfileId = "profile-1",
            ActivityItemId = "activity-1",
        };
    }
}
