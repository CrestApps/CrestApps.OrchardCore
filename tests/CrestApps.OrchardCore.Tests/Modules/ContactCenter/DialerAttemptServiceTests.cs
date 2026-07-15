using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Modules;
using YesSql;

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
        var queueService = new Mock<IActivityQueueService>();
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync((OmnichannelActivity)null);

        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            activityManager,
            voiceCallRouter,
            queueService: queueService);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        reservationService.Verify(
            service => service.CompensateAsync("r1", true, It.IsAny<CancellationToken>()),
            Times.Once);
        voiceCallRouter.Verify(p => p.RouteOutboundAsync(It.IsAny<ContactCenterDialRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_WhenEligibleAndProviderSucceeds_StartsCallAndMarksRinging()
    {
        // Arrange
        var reservation = Reservation();
        var interaction = new Interaction { ItemId = "int1" };
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+15551112222",
            Status = ActivityStatus.Pending,
        };
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(s => s.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var providerCommandStateService = CreateProviderCommandStateService();
        var voiceCallRouter = CreateVoiceCallRouter(Success("call1", "Default Asterisk"));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(interaction),
            CreateActivityManager(activity),
            voiceCallRouter,
            providerCommandStateService: providerCommandStateService);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started);
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Equal("Default Asterisk", interaction.ProviderName);
        Assert.Equal(ActivityStatus.Dialing, activity.Status);
        reservationService.Verify(s => s.AcceptAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
        providerCommandStateService.Verify(
            service => service.StageConfirmSentAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                "call1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryDialAsync_WhenSuccessSettlementLosesFence_TreatsAttemptAsOwnedElsewhere()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var interaction = new Interaction { ItemId = "int1" };
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+15551112222",
            Status = ActivityStatus.Pending,
        };
        var providerCommandStateService = CreateProviderCommandStateService();
        providerCommandStateService
            .Setup(service => service.StageConfirmSentAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                "call1",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCommandFenceException("int1", 2, 1));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(interaction),
            CreateActivityManager(activity),
            CreateVoiceCallRouter(Success("call1")),
            providerCommandStateService: providerCommandStateService);

        // Act
        var accepted = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(accepted);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        reservationService.Verify(
            value => value.CompensateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_WhenRecoveryAlreadySettledSuccess_TreatsAttemptAsOwnedElsewhere()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var providerCommandStateService = CreateProviderCommandStateService();
        providerCommandStateService
            .Setup(service => service.StageConfirmSentAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                "call1",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCommandTransitionException(
                "int1",
                ProviderCommandStatus.Confirmed,
                ProviderCommandStatus.Confirmed));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            CreateActivityManager(new OmnichannelActivity
            {
                ItemId = "act1",
                PreferredDestination = "+15551112222",
            }),
            CreateVoiceCallRouter(Success("call1")),
            providerCommandStateService: providerCommandStateService);

        // Act
        var accepted = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(accepted);
    }

    [Fact]
    public async Task TryDialAsync_WhenStaleProviderFailureLosesFence_DoesNotCompensateNewerOwner()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var interaction = new Interaction { ItemId = "int1" };
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+15551112222",
            Status = ActivityStatus.Pending,
        };
        var providerCommandStateService = CreateProviderCommandStateService();
        providerCommandStateService
            .Setup(service => service.BeginCompensationAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCommandFenceException("int1", 2, 1));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(interaction),
            CreateActivityManager(activity),
            CreateVoiceCallRouter(Failure("provider_rejected", "Provider rejected the request.")),
            providerCommandStateService: providerCommandStateService);

        // Act
        var accepted = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(accepted);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        reservationService.Verify(
            value => value.CompensateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_WhenUnknownSettlementAlreadyTransitioned_DoesNotProjectStaleOutcome()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var interaction = new Interaction { ItemId = "int1" };
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+15551112222",
            Status = ActivityStatus.Pending,
        };
        var providerCommandStateService = CreateProviderCommandStateService();
        providerCommandStateService
            .Setup(service => service.StageOutcomeUnknownAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCommandTransitionException(
                "int1",
                ProviderCommandStatus.Paused,
                ProviderCommandStatus.OutcomeUnknown));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(interaction),
            CreateActivityManager(activity),
            CreateVoiceCallRouter(new ContactCenterVoiceProviderResult
            {
                OutcomeUnknown = true,
                ErrorCode = "provider_outcome_unknown",
                ErrorMessage = "The provider response was lost.",
            }),
            providerCommandStateService: providerCommandStateService);

        // Act
        var accepted = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(accepted);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        reservationService.Verify(
            value => value.CompensateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_WhenRecoveryClaimsRegisteredCommand_TreatsAttemptAsOwnedElsewhere()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var providerCommandStateService = CreateProviderCommandStateService();
        providerCommandStateService
            .Setup(service => service.TryClaimAsync(
                "int1",
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderCommandClaim)null);
        var router = CreateVoiceCallRouter(Success("call1"));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            CreateActivityManager(new OmnichannelActivity
            {
                ItemId = "act1",
                PreferredDestination = "+15551112222",
            }),
            router,
            providerCommandStateService: providerCommandStateService);

        // Act
        var accepted = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(accepted);
        router.Verify(
            value => value.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_WhenMarkSentLosesDatabaseRace_TreatsAttemptAsOwnedElsewhere()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var providerCommandStateService = CreateProviderCommandStateService();
        providerCommandStateService
            .Setup(service => service.MarkSentAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));
        var router = CreateVoiceCallRouter(Success("call1"));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            CreateActivityManager(new OmnichannelActivity
            {
                ItemId = "act1",
                PreferredDestination = "+15551112222",
            }),
            router,
            providerCommandStateService: providerCommandStateService);

        // Act
        var accepted = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(accepted);
        router.Verify(
            value => value.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_AcceptsReservationBeforeInvokingProvider()
    {
        // Arrange
        var order = new List<string>();
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("accept"))
            .ReturnsAsync(reservation);
        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));
        voiceCallRouter
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "test",
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("route"))
            .ReturnsAsync(Success("call1"));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            CreateActivityManager(new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" }),
            voiceCallRouter);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started);
        Assert.Equal(["accept", "route"], order);
    }

    [Fact]
    public async Task TryDialAsync_WhenProviderFails_CancelsReservationAndMarksInteractionFailed()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        reservationService
            .Setup(service => service.CompensateAsync("r1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var queueService = new Mock<IActivityQueueService>();
        var interaction = new Interaction { ItemId = "int1" };
        var providerCommandStateService = CreateProviderCommandStateService();
        var voiceCallRouter = CreateVoiceCallRouter(Failure("provider_failed", "Provider rejected the request."));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(interaction),
            CreateActivityManager(new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" }),
            voiceCallRouter,
            queueService: queueService,
            providerCommandStateService: providerCommandStateService);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        reservationService.Verify(
            service => service.CompensateAsync("r1", true, It.IsAny<CancellationToken>()),
            Times.Once);
        providerCommandStateService.Verify(
            service => service.BeginCompensationAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                "Provider rejected the request.",
                It.IsAny<CancellationToken>()),
            Times.Once);
        providerCommandStateService.Verify(
            service => service.CompleteCompensationAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryDialAsync_WhenReservationNoLongerAvailable_NeverDialsProviderOrMutatesCurrentOwner()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityReservation)null);
        var interaction = new Interaction { ItemId = "int1" };
        var interactionManager = CreateInteractionManager(interaction);
        var activity = new OmnichannelActivity
        {
            ItemId = "act1",
            PreferredDestination = "+15551112222",
            Status = ActivityStatus.Pending,
        };
        var voiceCallRouter = CreateVoiceCallRouter(Success("provider-call-1"));
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
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        voiceCallRouter.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "test",
                It.IsAny<CancellationToken>()),
            Times.Never);
        reservationService.Verify(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
        reservationService.Verify(
            service => service.CancelAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        reservationService.Verify(
            service => service.CompensateAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        interactionManager.Verify(
            manager => manager.CreateAsync(
                It.IsAny<Interaction>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_PersistsIdempotentCommandIdBeforeInvokingProvider()
    {
        // Arrange
        var order = new List<string>();
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var interaction = new Interaction { ItemId = "int1" };
        var interactionManager = CreateInteractionManager(interaction);
        interactionManager
            .Setup(manager => manager.CreateAsync(
                interaction,
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("create"))
            .Returns(ValueTask.CompletedTask);
        var providerCommandStateService = CreateProviderCommandStateService();
        providerCommandStateService
            .Setup(service => service.RegisterAsync(
                It.IsAny<ProviderCommandRegistration>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit"))
            .ReturnsAsync(new ProviderCommand
            {
                CommandId = "int1",
                Status = ProviderCommandStatus.Pending,
            });
        providerCommandStateService
            .Setup(service => service.MarkSentAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("sent"))
            .ReturnsAsync(new ProviderCommand
            {
                CommandId = "int1",
                Status = ProviderCommandStatus.Sent,
            });
        ContactCenterDialRequest capturedRequest = null;
        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));
        voiceCallRouter
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "test",
                It.IsAny<CancellationToken>()))
            .Callback<ContactCenterDialRequest, string, CancellationToken>((request, _, _) =>
            {
                order.Add("route");
                capturedRequest = request;
            })
            .ReturnsAsync(Success("call1"));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            interactionManager,
            CreateActivityManager(new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" }),
            voiceCallRouter,
            providerCommandStateService: providerCommandStateService);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(started);
        Assert.Equal(["create", "commit", "sent", "route"], order);
        Assert.NotNull(capturedRequest);
        Assert.Equal("int1", capturedRequest.CommandId);
        Assert.Equal(
            capturedRequest.CommandId,
            capturedRequest.Metadata[ContactCenterConstants.CommandMetadata.CommandId]);
        Assert.Equal(
            capturedRequest.CommandId,
            capturedRequest.Metadata[TelephonyConstants.RequestMetadata.IdempotencyKey]);
        Assert.Equal("1", capturedRequest.Metadata[ContactCenterConstants.CommandMetadata.FenceToken]);
        Assert.Equal("1", capturedRequest.Metadata[TelephonyConstants.RequestMetadata.FenceToken]);
        Assert.Equal(
            capturedRequest.CommandId,
            interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId]);
        providerCommandStateService.Verify(
            state => state.RegisterAsync(
                It.Is<ProviderCommandRegistration>(registration =>
                    registration.CommandId == "int1" &&
                    registration.ReservationId == "r1" &&
                    registration.DialerProfileId == "profile1" &&
                    registration.ActivityItemId == "act1" &&
                    registration.InteractionId == "int1" &&
                    registration.CommandType == ProviderCommandType.Dial),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryDialAsync_WhenCommandIntentCommitFails_CompensatesInFreshScopeWithoutInvokingProvider()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var providerCommandStateService = CreateProviderCommandStateService();
        providerCommandStateService
            .Setup(service => service.RegisterAsync(
                It.IsAny<ProviderCommandRegistration>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The command intent could not be committed."));
        var scopedCompensation = new Mock<IDialerAttemptCompensationService>();
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        scopeExecutor
            .Setup(executor => executor.ExecuteAsync<IDialerAttemptCompensationService>(
                It.IsAny<Func<IDialerAttemptCompensationService, Task>>()))
            .Returns<Func<IDialerAttemptCompensationService, Task>>(operation =>
                operation(scopedCompensation.Object));
        var voiceCallRouter = CreateVoiceCallRouter(Success("call1"));
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(new Interaction { ItemId = "int1" }),
            CreateActivityManager(new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" }),
            voiceCallRouter,
            providerCommandStateService: providerCommandStateService,
            scopeExecutor: scopeExecutor);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        scopedCompensation.Verify(
            service => service.CompensateAsync(
                reservation,
                true,
                CancellationToken.None),
            Times.Once);
        voiceCallRouter.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryDialAsync_WhenProviderOutcomeIsUnknown_MarksUnknownWithoutCompensatingOrRedialing()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        reservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        reservationService
            .Setup(service => service.CompensateAsync("r1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var queueService = new Mock<IActivityQueueService>();
        var interaction = new Interaction { ItemId = "int1" };
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" };
        var providerCommandStateService = CreateProviderCommandStateService();
        var voiceCallRouter = CreateVoiceCallRouter(new ContactCenterVoiceProviderResult
        {
            Succeeded = false,
            OutcomeUnknown = true,
            ErrorCode = "dial_outcome_unknown",
            ErrorMessage = "The provider response was lost.",
        });
        var service = CreateService(
            EligibleGate(),
            reservationService,
            CreateInteractionManager(interaction),
            CreateActivityManager(activity),
            voiceCallRouter,
            queueService: queueService,
            providerCommandStateService: providerCommandStateService);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Equal("dial_outcome_unknown", interaction.TechnicalMetadata["providerErrorCode"]);
        Assert.Equal(ActivityStatus.Dialing, activity.Status);
        reservationService.Verify(
            service => service.CompensateAsync("r1", true, It.IsAny<CancellationToken>()),
            Times.Never);
        providerCommandStateService.Verify(
            service => service.StageOutcomeUnknownAsync(
                "int1",
                It.IsAny<ProviderCommandClaim>(),
                "The provider response was lost.",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryDialAsync_WhenSuppressedDoNotCall_MarksCancelledReleasesReservationAndAudits()
    {
        // Arrange
        var reservation = Reservation();
        var reservationService = new Mock<IActivityReservationService>();
        var activity = new OmnichannelActivity { ItemId = "act1", PreferredDestination = "+15551112222" };
        var publisher = new Mock<IContactCenterEventPublisher>();
        var queueService = new Mock<IActivityQueueService>();
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
            publisher,
            queueService: queueService);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(ActivityStatus.Cancelled, activity.Status);
        reservationService.Verify(
            service => service.CompensateAsync("r1", true, It.IsAny<CancellationToken>()),
            Times.Once);
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
        var queueService = new Mock<IActivityQueueService>();

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
            publisher,
            queueService: queueService);

        // Act
        var started = await service.TryDialAsync(CreateProfile(), reservation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Equal(ActivityStatus.Pending, activity.Status);
        activityManager.Verify(m => m.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        reservationService.Verify(
            service => service.CompensateAsync("r1", false, It.IsAny<CancellationToken>()),
            Times.Once);
        publisher.Verify(p => p.PublishAsync(It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.DialSuppressed), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ActivityReservation Reservation()
    {
        return new ActivityReservation { ItemId = "r1", ActivityItemId = "act1", QueueItemId = "qi1", AgentId = "a1" };
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

    private static Mock<IVoiceContactCenterCallRouter> CreateVoiceCallRouter(Exception exception)
    {
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>();
        voiceCallRouter
            .Setup(router => router.GetOutboundProviderName("test"))
            .Returns("test");
        voiceCallRouter
            .Setup(router => router.RouteOutboundAsync(It.IsAny<ContactCenterDialRequest>(), "test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        return voiceCallRouter;
    }

    private static DialerAttemptService CreateService(
        Mock<IDialerEligibilityService> eligibilityService,
        Mock<IActivityReservationService> reservationService,
        Mock<IInteractionManager> interactionManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IVoiceContactCenterCallRouter> voiceCallRouter,
        Mock<IContactCenterEventPublisher> publisher = null,
        Mock<IQueueItemManager> queueItemManager = null,
        Mock<IActivityQueueService> queueService = null,
        Mock<IProviderCommandStateService> providerCommandStateService = null,
        Mock<IContactCenterScopeExecutor> scopeExecutor = null,
        IDialerAttemptCompensationService compensationService = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        queueItemManager ??= new Mock<IQueueItemManager>();
        providerCommandStateService ??= CreateProviderCommandStateService();
        var session = new Mock<ISession>();
        session
            .Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        queueItemManager
            .Setup(m => m.FindByIdAsync("qi1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi1", Status = QueueItemStatus.Waiting });
        compensationService ??= new DialerAttemptCompensationService(reservationService.Object);

        return new DialerAttemptService(
            eligibilityService.Object,
            reservationService.Object,
            compensationService,
            interactionManager.Object,
            activityManager.Object,
            voiceCallRouter.Object,
            (publisher ?? new Mock<IContactCenterEventPublisher>()).Object,
            (scopeExecutor ?? new Mock<IContactCenterScopeExecutor>()).Object,
            providerCommandStateService.Object,
            session.Object,
            clock.Object,
            new Mock<ILogger<DialerAttemptService>>().Object);
    }

    private static Mock<IProviderCommandStateService> CreateProviderCommandStateService()
    {
        var service = new Mock<IProviderCommandStateService>();
        var claim = new ProviderCommandClaim
        {
            CommandId = "int1",
            FenceToken = 1,
            OwnerToken = "owner-1",
            LeaseExpiresUtc = _now.AddMinutes(2),
        };

        service
            .Setup(value => value.RegisterAsync(
                It.IsAny<ProviderCommandRegistration>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCommand
            {
                CommandId = "int1",
                Status = ProviderCommandStatus.Pending,
            });
        service
            .Setup(value => value.TryClaimAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
        service
            .Setup(value => value.MarkSentAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCommand { CommandId = "int1", Status = ProviderCommandStatus.Sent });
        service
            .Setup(value => value.StageConfirmSentAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCommand { CommandId = "int1", Status = ProviderCommandStatus.Confirmed });
        service
            .Setup(value => value.StageOutcomeUnknownAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCommand { CommandId = "int1", Status = ProviderCommandStatus.OutcomeUnknown });
        service
            .Setup(value => value.BeginCompensationAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCommand { CommandId = "int1", Status = ProviderCommandStatus.Compensating });
        service
            .Setup(value => value.TryClaimCompensationAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
        service
            .Setup(value => value.CompleteCompensationAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCommand { CommandId = "int1", Status = ProviderCommandStatus.Compensated });

        return service;
    }

    private static ContactCenterVoiceProviderResult Success(string providerCallId, string providerName = null)
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = providerCallId,
            ProviderName = providerName,
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
