using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerAttemptServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TryDialAsync_WhenActivityMissing_CancelsReservationWithoutDialing()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync((OmnichannelActivity)null);

        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            activityManager,
            voiceCallRouter);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        reservationService.Verify(s => s.CancelAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
        voiceCallRouter.Verify(p => p.RouteOutboundAsync(It.IsAny<ContactCenterDialRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_WhenEligibleAndProviderSucceeds_StartsCallAndMarksRinging()
    {
        // Arrange
        var reservation = Reservation();
        var interaction = new Interaction { ItemId = "int1" };
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" };
        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));
        var service = CreateService(
            EligibleGate(),
            new Mock<IActivityReservationService>(),
            CreateInteractionManager(interaction),
            CreateActivityManager(activity),
            voiceCallRouter);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started);
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Equal(ActivityStatus.Dialing, activity.Status);
    }

    [Fact]
    public async Task TryDialAsync_WhenProviderFails_CancelsReservationAndMarksInteractionFailed()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        var interaction = new Interaction { ItemId = "int1" };
        var voiceCallRouter = CreateVoiceCallRouter(Failure("provider_failed", "Provider rejected the request."));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(interaction),
            CreateActivityManager(new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" }),
            voiceCallRouter);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        reservationService.Verify(s => s.CancelAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryDialAsync_WhenSuppressedDoNotCall_MarksCancelledReleasesReservationAndAudits()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" };
        var publisher = new Mock<IContactCenterEventPublisher>();
        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));

        var gate = new Mock<IDialerEligibilityService>();
        gate
            .Setup(g => g.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Suppressed(DialerSuppressionReason.DoNotCall, "The contact opted out of phone calls."));

        var service = CreateService(
            gate,
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            CreateActivityManager(activity),
            voiceCallRouter,
            publisher);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(ActivityStatus.Cancelled, activity.Status);
        reservationService.Verify(s => s.CancelAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
        voiceCallRouter.Verify(p => p.RouteOutboundAsync(It.IsAny<ContactCenterDialRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(p => p.PublishAsync(It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.DialSuppressed), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryDialAsync_WhenSuppressedOutsideCallingWindow_ReleasesReservationWithoutChangingStatus()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222", Status = ActivityStatus.Pending };
        var activityManager = CreateActivityManager(activity);
        var publisher = new Mock<IContactCenterEventPublisher>();

        var gate = new Mock<IDialerEligibilityService>();
        gate
            .Setup(g => g.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Suppressed(DialerSuppressionReason.OutsideCallingWindow, "Outside the calling window."));

        var service = CreateService(
            gate,
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            activityManager,
            CreateVoiceCallRouter(Success("call1")),
            publisher);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        activityManager.Verify(m => m.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        reservationService.Verify(s => s.CancelAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(p => p.PublishAsync(It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.DialSuppressed), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ActivityReservation Reservation()
    {
        return new ActivityReservation { ItemId = "r1", ActivityItemId = "act1", AgentId = "a1" };
    }

    private static DialerProfile CreateProfile()
    {
        return new DialerProfile
        {
            ItemId = "profile1",
            Name = "Power",
            QueueId = "q1",
            ProviderName = "test",
            Mode = DialerMode.Power,
            MaxAttempts = 3,
            Enabled = true,
        };
    }

    private static Mock<IDialerEligibilityService> EligibleGate()
    {
        var gate = new Mock<IDialerEligibilityService>();
        gate
            .Setup(g => g.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Eligible());

        return gate;
    }

    private static Mock<IInteractionManager> CreateInteractionManager(Interaction interaction)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        return interactionManager;
    }

    private static Mock<IOmnichannelActivityManager> CreateActivityManager(OmnichannelActivity activity)
    {
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        return activityManager;
    }

    private static Mock<IVoiceContactCenterCallRouter> CreateVoiceCallRouter(ContactCenterVoiceProviderResult result)
    {
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>();
        voiceCallRouter
            .Setup(router => router.GetOutboundProviderName("test"))
            .Returns("test");
        voiceCallRouter
            .Setup(router => router.RouteOutboundAsync(It.IsAny<ContactCenterDialRequest>(), "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return voiceCallRouter;
    }

    private static DialerAttemptService CreateService(
        Mock<IDialerEligibilityService> eligibilityService,
        Mock<IActivityReservationService> reservationService,
        Mock<IInteractionManager> interactionManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IVoiceContactCenterCallRouter> voiceCallRouter,
        Mock<IContactCenterEventPublisher> publisher = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DialerAttemptService(
            eligibilityService.Object,
            reservationService.Object,
            interactionManager.Object,
            activityManager.Object,
            voiceCallRouter.Object,
            (publisher ?? new Mock<IContactCenterEventPublisher>()).Object,
            clock.Object,
            new Mock<ILogger<DialerAttemptService>>().Object);
    }

    private static ContactCenterVoiceProviderResult Success(string providerCallId)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = providerCallId,
        };
    }

    private static ContactCenterVoiceProviderResult Failure(string errorCode, string errorMessage)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };
    }
}
