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

public sealed class DialerServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunCycleAsync_WhenProviderFails_CancelsReservationAndMarksInteractionFailed()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", ActivityItemId = "act1", AgentId = "a1" };
        var assignmentService = CreateAssignmentService(reservation);
        var reservationService = new Mock<IActivityReservationService>();
        var interaction = new Interaction { ItemId = "int1" };
        var interactionManager = CreateInteractionManager(interaction);
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" };
        var activityManager = CreateActivityManager(activity);
        var voiceCallRouter = CreateVoiceCallRouter(Failure("provider_failed", "Provider rejected the request."));
        var service = CreateService(assignmentService, reservationService, interactionManager, activityManager, voiceCallRouter);

        // Act
        var started = await service.RunCycleAsync(CreateProfile(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, started);
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        reservationService.Verify(s => s.CancelAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunCycleAsync_WhenMaxAttemptsReached_CancelsReservationWithoutDialing()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", ActivityItemId = "act1", AgentId = "a1" };
        var assignmentService = CreateAssignmentService(reservation);
        var reservationService = new Mock<IActivityReservationService>();
        var interactionManager = CreateInteractionManager(new Interaction { ItemId = "int1" });
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+15551112222",
            Attempts = 3,
        };

        var activityManager = CreateActivityManager(activity);
        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));
        var service = CreateService(assignmentService, reservationService, interactionManager, activityManager, voiceCallRouter);

        // Act
        var started = await service.RunCycleAsync(CreateProfile(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, started);
        Assert.Equal(ActivityStatus.Failed, activity.Status);
        voiceCallRouter.Verify(p => p.RouteOutboundAsync(It.IsAny<ContactCenterDialRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        reservationService.Verify(s => s.CancelAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunCycleAsync_WhenPowerMode_StopsAtCallsPerAgentPacingLimit()
    {
        // Arrange
        var firstReservation = new ActivityReservation { ItemId = "r1", ActivityItemId = "act1", AgentId = "a1" };
        var secondReservation = new ActivityReservation { ItemId = "r2", ActivityItemId = "act1", AgentId = "a2" };
        var assignmentService = new Mock<IActivityAssignmentService>();
        assignmentService
            .SetupSequence(s => s.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstReservation)
            .ReturnsAsync(secondReservation)
            .ReturnsAsync((ActivityReservation)null);

        var reservationService = new Mock<IActivityReservationService>();
        var interactionManager = CreateInteractionManager(new Interaction { ItemId = "int1" });
        var activityManager = CreateActivityManager(new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" });
        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));
        var service = CreateService(assignmentService, reservationService, interactionManager, activityManager, voiceCallRouter);
        var profile = CreateProfile();
        profile.CallsPerAgent = 1;

        // Act
        var started = await service.RunCycleAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, started);
        assignmentService.Verify(s => s.AssignNextAsync("q1", It.IsAny<CancellationToken>()), Times.Once);
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

    private static Mock<IActivityAssignmentService> CreateAssignmentService(ActivityReservation reservation)
    {
        var assignmentService = new Mock<IActivityAssignmentService>();
        assignmentService
            .SetupSequence(s => s.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation)
            .ReturnsAsync((ActivityReservation)null);

        return assignmentService;
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
            .Setup(router => router.CanRouteOutbound("test"))
            .Returns(true);
        voiceCallRouter
            .Setup(router => router.GetOutboundProviderName("test"))
            .Returns("test");
        voiceCallRouter
            .Setup(router => router.RouteOutboundAsync(It.IsAny<ContactCenterDialRequest>(), "test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return voiceCallRouter;
    }

    private static DialerService CreateService(
        Mock<IActivityAssignmentService> assignmentService,
        Mock<IActivityReservationService> reservationService,
        Mock<IInteractionManager> interactionManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IVoiceContactCenterCallRouter> voiceCallRouter)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DialerService(
            assignmentService.Object,
            reservationService.Object,
            interactionManager.Object,
            activityManager.Object,
            voiceCallRouter.Object,
            new Mock<IContactCenterEventPublisher>().Object,
            clock.Object,
            new Mock<ILogger<DialerService>>().Object);
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
