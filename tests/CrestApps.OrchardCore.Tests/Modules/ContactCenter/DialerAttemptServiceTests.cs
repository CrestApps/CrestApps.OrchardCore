#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerAttemptServiceTests
{
    [Fact]
    public async Task TryDialAsync_WhenActivityMissing_CancelsReservationWithoutPersistingAttempt()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        reservationService
            .Setup(service => service.CompensateAsync("r1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>(MockBehavior.Strict);

        var service = CreateService(
            EligibleGate(),
            reservationService,
            interactionManager,
            CreateActivityManager(null),
            voiceCallRouter);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        reservationService.Verify(
            service => service.CompensateAsync("r1", true, It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoOutboundRouting(voiceCallRouter);
    }

    [Fact]
    public async Task TryDialAsync_WhenReservationIsNotAccepted_ReturnsFalseWithoutPersistingIntent()
    {
        // Arrange
        var reservation = Reservation();
        var activity = CreateActivity();
        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityReservation?)null);
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>(MockBehavior.Strict);

        var service = CreateService(
            EligibleGate(),
            reservationService,
            interactionManager,
            CreateActivityManager(activity),
            voiceCallRouter);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(4, activity.Attempts);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        reservationService.Verify(
            service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoOutboundRouting(voiceCallRouter);
    }

    [Fact]
    public async Task TryDialAsync_WhenEligible_PersistsAttemptRegistersIntentPublishesAndSchedulesDispatch()
    {
        // Arrange
        var order = new List<string>();
        var reservation = Reservation();
        var activity = CreateActivity();
        var interaction = CreateInteraction();
        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        ProviderCommandRegistration? capturedRegistration = null;
        providerCommandStateService
            .Setup(service => service.RegisterAsync(
                It.IsAny<ProviderCommandRegistration>(),
                It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) =>
            {
                capturedRegistration = registration;
                order.Add("register");
            })
            .ReturnsAsync(new ProviderCommand
            {
                CommandId = interaction.ItemId,
                Status = ProviderCommandStatus.Pending,
            });

        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("accept"))
            .ReturnsAsync(reservation);
        var voiceCallRouter = CreateVoiceCallRouter();

        var activityManager = CreateActivityManager(activity);
        activityManager
            .Setup(manager => manager.UpdateAsync(
                activity,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("update"))
            .Returns(ValueTask.CompletedTask);

        var interactionManager = CreateInteractionManager(interaction);
        interactionManager
            .Setup(manager => manager.CreateAsync(interaction, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("create"))
            .Returns(ValueTask.CompletedTask);

        InteractionEvent? publishedEvent = null;
        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                publishedEvent = interactionEvent;
                order.Add("publish");
            })
            .Returns(Task.CompletedTask);

        Func<IProviderCommandProcessor, Task>? scheduledDispatch = null;
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        scopeExecutor
            .Setup(executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Callback<Func<IProviderCommandProcessor, Task>>(operation =>
            {
                scheduledDispatch = operation;
                order.Add("schedule");
            })
            .Returns(true);

        var processor = new Mock<IProviderCommandProcessor>(MockBehavior.Strict);
        processor
            .Setup(value => value.DispatchAsync(interaction.ItemId, CancellationToken.None))
            .ReturnsAsync(new ProviderCommand
            {
                CommandId = interaction.ItemId,
                Status = ProviderCommandStatus.Sent,
            });

        var service = CreateService(
            EligibleGate(),
            reservationService,
            interactionManager,
            activityManager,
            voiceCallRouter,
            publisher,
            scopeExecutor,
            providerCommandStateService);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started);
        Assert.Equal(["accept", "update", "create", "publish", "register", "schedule"], order);
        Assert.Equal(5, activity.Attempts);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        Assert.Equal(InteractionChannel.Voice, interaction.Channel);
        Assert.Equal(InteractionDirection.Outbound, interaction.Direction);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Equal(activity.ItemId, interaction.ActivityItemId);
        Assert.Equal("q1", interaction.QueueId);
        Assert.Equal(reservation.AgentId, interaction.AgentId);
        Assert.Equal("test", interaction.ProviderName);
        Assert.Equal(activity.PreferredDestination, interaction.CustomerAddress);
        Assert.Equal(interaction.ItemId, interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId]);
        var registration = Assert.IsType<ProviderCommandRegistration>(capturedRegistration);
        Assert.Equal(interaction.ItemId, registration.CommandId);
        Assert.Equal("test", registration.ProviderName);
        Assert.Equal(ProviderCommandType.Dial, registration.CommandType);
        Assert.Equal(activity.ItemId, registration.ActivityItemId);
        Assert.Equal(interaction.ItemId, registration.InteractionId);
        Assert.Equal(reservation.ItemId, registration.ReservationId);
        Assert.Equal("profile1", registration.DialerProfileId);
        Assert.False(string.IsNullOrWhiteSpace(registration.RequestPayload));
        var request = System.Text.Json.JsonSerializer.Deserialize<ContactCenterDialRequest>(registration.RequestPayload);

        Assert.Equal("user-a1", request?.AgentUserId);

        var eventRecord = Assert.IsType<InteractionEvent>(publishedEvent);
        Assert.Equal(ContactCenterConstants.Events.DialerAttemptStarted, eventRecord.EventType);
        Assert.Equal(nameof(DialerProfile), eventRecord.AggregateType);
        Assert.Equal(CreateProfile().ItemId, eventRecord.AggregateId);
        Assert.Equal(ContactCenterConstants.Components.Dialer, eventRecord.SourceComponent);
        Assert.Equal(interaction.ItemId, eventRecord.InteractionId);
        Assert.Equal($"dialer-attempt:{interaction.ItemId}", eventRecord.IdempotencyKey);
        var dispatch = Assert.IsType<Func<IProviderCommandProcessor, Task>>(scheduledDispatch);
        await dispatch(processor.Object);
        processor.Verify(
            value => value.DispatchAsync(interaction.ItemId, CancellationToken.None),
            Times.Once);
        reservationService.Verify(
            value => value.AcceptAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        voiceCallRouter.Verify(
            router => router.GetOutboundProviderName("test"),
            Times.Once);
        scopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Once);
        VerifyNoOutboundRouting(voiceCallRouter);
        VerifyNoInlineProviderCommandCalls(providerCommandStateService);
    }

    [Fact]
    public async Task TryDialAsync_WhenProviderCommandRegistrationFails_CompensatesInFreshScopeAndRethrows()
    {
        // Arrange
        var order = new List<string>();
        var reservation = Reservation();
        var activity = CreateActivity();
        var interaction = CreateInteraction();
        var voiceCallRouter = CreateVoiceCallRouter();
        InteractionEvent? publishedEvent = null;

        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("accept"))
            .ReturnsAsync(reservation);

        var activityManager = CreateActivityManager(activity);
        activityManager
            .Setup(manager => manager.UpdateAsync(
                activity,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("update"))
            .Returns(ValueTask.CompletedTask);

        var interactionManager = CreateInteractionManager(interaction);
        interactionManager
            .Setup(manager => manager.CreateAsync(interaction, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("create"))
            .Returns(ValueTask.CompletedTask);

        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                publishedEvent = interactionEvent;
                order.Add("publish");
            })
            .Returns(Task.CompletedTask);

        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        providerCommandStateService
            .Setup(service => service.RegisterAsync(
                It.IsAny<ProviderCommandRegistration>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("register"))
            .ThrowsAsync(new InvalidOperationException("The command intent could not be committed."));

        var scopedCompensation = new Mock<IDialerAttemptCompensationService>(MockBehavior.Strict);
        scopedCompensation
            .Setup(service => service.CompensateAsync(reservation, true, CancellationToken.None))
            .Callback(() => order.Add("compensate"))
            .Returns(Task.CompletedTask);

        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        scopeExecutor
            .Setup(executor => executor.ExecuteAsync<IDialerAttemptCompensationService>(
                It.IsAny<Func<IDialerAttemptCompensationService, Task>>()))
            .Returns<Func<IDialerAttemptCompensationService, Task>>(operation =>
                operation(scopedCompensation.Object));

        var service = CreateService(
            EligibleGate(),
            reservationService,
            interactionManager,
            activityManager,
            voiceCallRouter,
            publisher,
            scopeExecutor: scopeExecutor,
            providerCommandStateService: providerCommandStateService);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(["accept", "update", "create", "publish", "register", "compensate"], order);
        Assert.Equal(5, activity.Attempts);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        var eventRecord = Assert.IsType<InteractionEvent>(publishedEvent);
        Assert.Equal($"dialer-attempt:{interaction.ItemId}", eventRecord.IdempotencyKey);
        scopedCompensation.Verify(
            service => service.CompensateAsync(reservation, true, CancellationToken.None),
            Times.Once);
        scopeExecutor.Verify(
            executor => executor.ExecuteAsync<IDialerAttemptCompensationService>(
                It.IsAny<Func<IDialerAttemptCompensationService, Task>>()),
            Times.Once);
        scopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
        voiceCallRouter.Verify(
            router => router.GetOutboundProviderName("test"),
            Times.Once);
        VerifyNoOutboundRouting(voiceCallRouter);
        VerifyNoInlineProviderCommandCalls(providerCommandStateService);
    }

    [Theory]
    [InlineData(DialerSuppressionReason.DoNotCall, ActivityStatus.Cancelled, true)]
    [InlineData(DialerSuppressionReason.OutsideCallingWindow, null, false)]
    [InlineData(DialerSuppressionReason.NoDestination, ActivityStatus.Failed, true)]
    public async Task TryDialAsync_WhenEligibilitySuppresses_CompensatesAndPublishesSuppressionEvent(
        DialerSuppressionReason reason,
        ActivityStatus? expectedStatus,
        bool removeFromQueue)
    {
        // Arrange
        var order = new List<string>();
        var reservation = Reservation();
        var activity = CreateActivity();
        var activityManager = CreateActivityManager(activity);
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>(MockBehavior.Strict);
        var reservationService = new Mock<IActivityReservationService>(MockBehavior.Strict);
        reservationService
            .Setup(service => service.CompensateAsync("r1", removeFromQueue, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("compensate"))
            .ReturnsAsync(reservation);

        if (expectedStatus.HasValue)
        {
            activityManager
                .Setup(manager => manager.UpdateAsync(
                    activity,
                    It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => order.Add("update"))
                .Returns(ValueTask.CompletedTask);
        }

        InteractionEvent? publishedEvent = null;
        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                publishedEvent = interactionEvent;
                order.Add("publish");
            })
            .Returns(Task.CompletedTask);

        var service = CreateService(
            SuppressedGate(reason),
            reservationService,
            interactionManager,
            activityManager,
            voiceCallRouter,
            publisher);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(4, activity.Attempts);

        if (expectedStatus.HasValue)
        {
            Assert.Equal(expectedStatus.Value, activity.Status);
            Assert.Equal(["update", "compensate", "publish"], order);
            activityManager.Verify(
                manager => manager.UpdateAsync(
                    activity,
                    It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        else
        {
            Assert.Equal(ActivityStatus.Pending, activity.Status);
            Assert.Equal(["compensate", "publish"], order);
            activityManager.Verify(
                manager => manager.UpdateAsync(
                    It.IsAny<OmnichannelActivity>(),
                    It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        reservationService.Verify(
            service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()),
            Times.Never);
        reservationService.Verify(
            service => service.CompensateAsync("r1", removeFromQueue, It.IsAny<CancellationToken>()),
            Times.Once);
        var eventRecord = Assert.IsType<InteractionEvent>(publishedEvent);
        Assert.Equal(ContactCenterConstants.Events.DialSuppressed, eventRecord.EventType);
        Assert.Equal(nameof(OmnichannelActivity), eventRecord.AggregateType);
        Assert.Equal(activity.ItemId, eventRecord.AggregateId);
        Assert.Equal(ContactCenterConstants.Components.Dialer, eventRecord.SourceComponent);

        var suppression = Assert.IsType<DialerSuppressionEventData>(eventRecord.GetData<DialerSuppressionEventData>());
        Assert.Equal(CreateProfile().ItemId, suppression.ProfileItemId);
        Assert.Equal(activity.ItemId, suppression.ActivityItemId);
        Assert.Equal(reason, suppression.Reason);
        Assert.Equal("Suppressed by policy.", suppression.Description);
        Assert.Equal(activity.PreferredDestination, suppression.Destination);
        VerifyNoOutboundRouting(voiceCallRouter);
    }

    private static ActivityReservation Reservation()
    {
        return new ActivityReservation
        {
            ItemId = "r1",
            ActivityItemId = "act1",
            QueueItemId = "qi1",
            AgentId = "a1",
        };
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

    private static OmnichannelActivity CreateActivity()
    {
        return new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+15551112222",
            Status = ActivityStatus.Pending,
            Attempts = 4,
        };
    }

    private static Interaction CreateInteraction()
    {
        return new Interaction
        {
            ItemId = "int1",
        };
    }

    private static Mock<IDialerEligibilityService> EligibleGate()
    {
        var gate = new Mock<IDialerEligibilityService>(MockBehavior.Strict);
        gate
            .Setup(g => g.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Eligible());

        return gate;
    }

    private static Mock<IDialerEligibilityService> SuppressedGate(DialerSuppressionReason reason)
    {
        var gate = new Mock<IDialerEligibilityService>(MockBehavior.Strict);
        gate
            .Setup(g => g.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialerEligibilityResult.Suppressed(reason, "Suppressed by policy."));

        return gate;
    }

    private static Mock<IInteractionManager> CreateInteractionManager(Interaction interaction)
    {
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        return interactionManager;
    }

    private static Mock<IOmnichannelActivityManager> CreateActivityManager(OmnichannelActivity? activity)
    {
        var activityManager = new Mock<IOmnichannelActivityManager>(MockBehavior.Strict);
        activityManager
            .Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity!);

        return activityManager;
    }

    private static Mock<IVoiceContactCenterCallRouter> CreateVoiceCallRouter(string providerName = "test")
    {
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>(MockBehavior.Strict);
        voiceCallRouter
            .Setup(router => router.GetOutboundProviderName("test"))
            .Returns(providerName);

        return voiceCallRouter;
    }

    private static DialerAttemptService CreateService(
        Mock<IDialerEligibilityService> eligibilityService,
        Mock<IActivityReservationService> reservationService,
        Mock<IInteractionManager> interactionManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IVoiceContactCenterCallRouter> voiceCallRouter,
        Mock<IContactCenterEventPublisher>? publisher = null,
        Mock<IContactCenterScopeExecutor>? scopeExecutor = null,
        Mock<IProviderCommandStateService>? providerCommandStateService = null,
        Mock<IAgentProfileManager>? agentManager = null,
        IDialerAttemptCompensationService? compensationService = null)
    {
        publisher ??= new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        scopeExecutor ??= new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        providerCommandStateService ??= new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        agentManager ??= CreateAgentManager();
        compensationService ??= new DialerAttemptCompensationService(reservationService.Object);

        return new DialerAttemptService(
            eligibilityService.Object,
            reservationService.Object,
            compensationService,
            interactionManager.Object,
            activityManager.Object,
            agentManager.Object,
            voiceCallRouter.Object,
            publisher.Object,
            scopeExecutor.Object,
            providerCommandStateService.Object,
            new Mock<ILogger<DialerAttemptService>>().Object);
    }

    private static Mock<IAgentProfileManager> CreateAgentManager()
    {
        var agentManager = new Mock<IAgentProfileManager>(MockBehavior.Strict);
        agentManager
            .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                UserId = "user-a1",
            });

        return agentManager;
    }

    private static void VerifyNoOutboundRouting(Mock<IVoiceContactCenterCallRouter> service)
    {
        service.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static void VerifyNoInlineProviderCommandCalls(Mock<IProviderCommandStateService> service)
    {
        service.Verify(
            value => value.TryClaimAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        service.Verify(
            value => value.TryClaimCompensationAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        service.Verify(
            value => value.MarkSentAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        service.Verify(
            value => value.StageConfirmSentAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        service.Verify(
            value => value.StageOutcomeUnknownAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        service.Verify(
            value => value.BeginCompensationAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        service.Verify(
            value => value.CompleteCompensationAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
