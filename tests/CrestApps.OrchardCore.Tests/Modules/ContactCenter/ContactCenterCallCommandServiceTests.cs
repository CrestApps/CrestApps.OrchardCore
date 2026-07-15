#nullable enable annotations

using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterCallCommandServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenInteractionIsMissing_AcceptsReservationWithoutMedia()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupPendingReservation();
        harness.InteractionManager
            .Setup(manager => manager.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null!);
        harness.ReservationService
            .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReservation());

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", "u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresDeviceAnswer);
        Assert.Null(result.InteractionId);
        Assert.Equal("The work was accepted.", result.Reason);
        harness.ReservationService.Verify(
            service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        harness.InteractionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.CallSessionManager.Verify(
            manager => manager.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.VoiceProviderResolver.Verify(
            resolver => resolver.Get(It.IsAny<string>()),
            Times.Never);
        harness.ProviderCommandStateService.Verify(
            service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ScopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenActivityIsPreviewDial_StartsDialerAttemptWithoutAcceptingReservation()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupPendingReservation();
        harness.SetupPreviewDialAttempt();

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", "u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresDeviceAnswer);
        harness.DialerAttemptService.Verify(
            attemptService => attemptService.TryDialAsync(
                It.Is<DialerProfile>(profile => profile.ItemId == "profile-1"),
                It.Is<ActivityReservation>(reservation => reservation.ItemId == "r1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        harness.ReservationService.Verify(
            service => service.AcceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.InteractionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.CallSessionManager.Verify(
            manager => manager.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.VoiceProviderResolver.Verify(
            resolver => resolver.Get(It.IsAny<string>()),
            Times.Never);
        harness.ProviderCommandStateService.Verify(
            service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(InteractionStatus.Ended)]
    [InlineData(InteractionStatus.Failed)]
    public async Task AcceptInboundOfferAsync_WhenDurableInteractionIsTerminal_ReturnsFailureWithoutAcceptingReservation(
        InteractionStatus terminalStatus)
    {
        // Arrange
        var harness = new Harness();
        harness.SetupPendingReservation();
        harness.SetupInteraction();
        harness.Interaction.Status = terminalStatus;

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", "u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The offer is no longer available.", result.Reason);
        harness.ReservationService.Verify(
            service => service.AcceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.InteractionManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.CallSessionManager.Verify(
            manager => manager.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.VoiceProviderResolver.Verify(
            resolver => resolver.Get(It.IsAny<string>()),
            Times.Never);
        harness.ProviderCommandStateService.Verify(
            service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ScopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenAgentDeviceNativeProvider_StagesRingingWithoutProviderCommand()
    {
        // Arrange
        var order = new List<string>();
        var harness = new Harness();
        ConfigureAcceptedInboundOffer(harness, order);
        harness.SetupProvider(VoiceProviderDeliveryModel.AgentDeviceNative);

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", "u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.RequiresDeviceAnswer);
        Assert.Equal("int1", result.InteractionId);
        Assert.Equal("session-1", result.CallSessionId);
        Assert.Equal(InteractionStatus.Ringing, harness.Interaction.Status);
        Assert.Equal("a1", harness.Interaction.AgentId);
        Assert.Equal("q1", harness.Interaction.QueueId);
        Assert.Equal(_now, harness.Interaction.StartedUtc);
        var createdCallSession = harness.CreatedCallSession!;

        Assert.Equal(ContactCenterCallState.Ringing, createdCallSession.State);
        Assert.Equal(VoiceProviderDeliveryModel.AgentDeviceNative, createdCallSession.DeliveryModel);
        Assert.Equal(_now, createdCallSession.CreatedUtc);
        Assert.Equal(_now, createdCallSession.StartedUtc);
        Assert.Null(createdCallSession.AnsweredUtc);
        Assert.Equal(
            [
                "accept",
                "interaction",
                "session",
                ContactCenterConstants.Events.CallSessionCreated,
                ContactCenterConstants.Events.OfferAccepted,
            ],
            order);
        harness.ReservationService.Verify(
            service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        harness.VoiceProviderResolver.Verify(
            resolver => resolver.Get("dp"),
            Times.Once);
        harness.Provider.Verify(
            provider => provider.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ProviderCommandStateService.Verify(
            service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ScopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenServerSideAcdProvider_RegistersAnswerCommandAndSchedulesProcessor()
    {
        // Arrange
        var order = new List<string>();
        var harness = new Harness();
        ConfigureAcceptedInboundOffer(harness, order);
        harness.SetupProvider(VoiceProviderDeliveryModel.ServerSideAcd);
        ProviderCommandRegistration? capturedRegistration = null;
        string? observedCommandIdDuringRegistration = null;
        Func<IProviderCommandProcessor, Task>? scheduledDispatch = null;
        harness.ProviderCommandStateService
            .Setup(service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) =>
            {
                capturedRegistration = registration;
                observedCommandIdDuringRegistration = Assert.IsType<string>(harness.Interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId]);
                order.Add("register");
            })
            .ReturnsAsync(new ProviderCommand());
        harness.ScopeExecutor
            .Setup(executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Callback<Func<IProviderCommandProcessor, Task>>(operation =>
            {
                scheduledDispatch = operation;
                order.Add("schedule");
            })
            .Returns(true);

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", "u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresDeviceAnswer);
        Assert.Equal("int1", result.InteractionId);
        Assert.Equal("session-1", result.CallSessionId);
        Assert.Equal(InteractionStatus.Ringing, harness.Interaction.Status);
        Assert.Equal(ContactCenterCallState.Ringing, harness.CreatedCallSession!.State);
        Assert.Equal(
            [
                "accept",
                "interaction",
                "session",
                ContactCenterConstants.Events.CallSessionCreated,
                ContactCenterConstants.Events.OfferAccepted,
                "register",
                "schedule",
            ],
            order);
        Assert.NotNull(capturedRegistration);
        Assert.Equal(capturedRegistration!.CommandId, observedCommandIdDuringRegistration);
        AssertAnswerRegistration(harness, capturedRegistration!);
        harness.ReservationService.Verify(
            service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        harness.VoiceProviderResolver.Verify(
            resolver => resolver.Get("dp"),
            Times.Once);
        harness.Provider.Verify(
            provider => provider.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ProviderCommandStateService.Verify(
            service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.ScopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Once);
        Assert.NotNull(scheduledDispatch);
        var processor = new Mock<IProviderCommandProcessor>();
        processor
            .Setup(value => value.DispatchAsync(capturedRegistration!.CommandId, CancellationToken.None))
            .ReturnsAsync(new ProviderCommand { CommandId = capturedRegistration!.CommandId });

        await scheduledDispatch!(processor.Object);

        processor.Verify(
            value => value.DispatchAsync(capturedRegistration!.CommandId, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenNoVoiceProviderIsResolved_RegistersAnswerCommandAndSchedulesProcessor()
    {
        // Arrange
        var order = new List<string>();
        var harness = new Harness();
        ConfigureAcceptedInboundOffer(harness, order);
        harness.SetupNoProvider();
        ProviderCommandRegistration? capturedRegistration = null;
        string? observedCommandIdDuringRegistration = null;
        Func<IProviderCommandProcessor, Task>? scheduledDispatch = null;
        harness.ProviderCommandStateService
            .Setup(service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) =>
            {
                capturedRegistration = registration;
                observedCommandIdDuringRegistration = Assert.IsType<string>(harness.Interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId]);
                order.Add("register");
            })
            .ReturnsAsync(new ProviderCommand());
        harness.ScopeExecutor
            .Setup(executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Callback<Func<IProviderCommandProcessor, Task>>(operation =>
            {
                scheduledDispatch = operation;
                order.Add("schedule");
            })
            .Returns(true);

        var service = harness.CreateService();

        // Act
        var result = await service.AcceptInboundOfferAsync("r1", "u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresDeviceAnswer);
        Assert.Equal("int1", result.InteractionId);
        Assert.Equal("session-1", result.CallSessionId);
        Assert.Equal(InteractionStatus.Ringing, harness.Interaction.Status);
        Assert.Equal(ContactCenterCallState.Ringing, harness.CreatedCallSession!.State);
        Assert.Equal(
            [
                "accept",
                "interaction",
                "session",
                ContactCenterConstants.Events.CallSessionCreated,
                ContactCenterConstants.Events.OfferAccepted,
                "register",
                "schedule",
            ],
            order);
        Assert.NotNull(capturedRegistration);
        Assert.Equal(capturedRegistration!.CommandId, observedCommandIdDuringRegistration);
        AssertAnswerRegistration(harness, capturedRegistration!);
        harness.ReservationService.Verify(
            service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        harness.VoiceProviderResolver.Verify(
            resolver => resolver.Get("dp"),
            Times.Once);
        harness.ProviderCommandStateService.Verify(
            service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.ScopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Once);
        Assert.NotNull(scheduledDispatch);
        var processor = new Mock<IProviderCommandProcessor>();
        processor
            .Setup(value => value.DispatchAsync(capturedRegistration!.CommandId, CancellationToken.None))
            .ReturnsAsync(new ProviderCommand { CommandId = capturedRegistration!.CommandId });

        await scheduledDispatch!(processor.Object);

        processor.Verify(
            value => value.DispatchAsync(capturedRegistration!.CommandId, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task AcceptInboundOfferAsync_WhenProviderCommandRegistrationFails_CompensatesReservationInFreshScope()
    {
        // Arrange
        var order = new List<string>();
        var harness = new Harness();
        ConfigureAcceptedInboundOffer(harness, order);
        harness.SetupProvider(VoiceProviderDeliveryModel.ServerSideAcd);
        ProviderCommandRegistration? capturedRegistration = null;
        string? observedCommandIdDuringRegistration = null;
        var compensationService = new Mock<IActivityReservationService>();
        compensationService
            .Setup(service => service.CompensateAsync("r1", false, CancellationToken.None))
            .Callback(() => order.Add("compensate"))
            .ReturnsAsync(CreateReservation());
        harness.ProviderCommandStateService
            .Setup(service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) =>
            {
                capturedRegistration = registration;
                observedCommandIdDuringRegistration = Assert.IsType<string>(harness.Interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId]);
                order.Add("register");
            })
            .ThrowsAsync(new InvalidOperationException("The command intent could not be committed."));
        harness.ScopeExecutor
            .Setup(executor => executor.ExecuteAsync<IActivityReservationService>(It.IsAny<Func<IActivityReservationService, Task>>()))
            .Returns<Func<IActivityReservationService, Task>>(operation => operation(compensationService.Object));

        var service = harness.CreateService();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcceptInboundOfferAsync("r1", "u1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("The command intent could not be committed.", exception.Message);
        Assert.NotNull(capturedRegistration);
        Assert.Equal(capturedRegistration!.CommandId, observedCommandIdDuringRegistration);
        AssertAnswerRegistration(harness, capturedRegistration!);
        Assert.Equal(
            [
                "accept",
                "interaction",
                "session",
                ContactCenterConstants.Events.CallSessionCreated,
                ContactCenterConstants.Events.OfferAccepted,
                "register",
                "compensate",
            ],
            order);
        harness.ReservationService.Verify(
            service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Provider.Verify(
            provider => provider.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ScopeExecutor.Verify(
            executor => executor.ExecuteAsync<IActivityReservationService>(It.IsAny<Func<IActivityReservationService, Task>>()),
            Times.Once);
        harness.ScopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
        harness.ProviderCommandStateService.Verify(
            service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Once);
        compensationService.Verify(
            service => service.CompensateAsync("r1", false, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task DeclineInboundOfferAsync_PublishesDurableOfferDeclinedEvent()
    {
        // Arrange
        var order = new List<string>();
        var harness = new Harness();
        harness.SetupPendingReservation();
        InteractionEvent? publishedEvent = null;
        harness.ReservationService
            .Setup(service => service.RejectAsync("r1", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("reject"))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1", AgentId = "a1", QueueId = "q1" });
        harness.Publisher
            .Setup(publisher => publisher.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                publishedEvent = interactionEvent;
                order.Add("publish");
            })
            .Returns(Task.CompletedTask);

        var service = harness.CreateService();

        // Act
        var result = await service.DeclineInboundOfferAsync("r1", "u1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.RequiresDeviceAnswer);
        Assert.Equal("The offer was declined.", result.Reason);
        Assert.Equal(["reject", "publish"], order);
        Assert.NotNull(publishedEvent);
        var declinedEvent = publishedEvent!;

        Assert.Equal(ContactCenterConstants.Events.OfferDeclined, declinedEvent.EventType);
        Assert.Equal(nameof(ActivityReservation), declinedEvent.AggregateType);
        Assert.Equal("r1", declinedEvent.AggregateId);
        Assert.Equal("a1", declinedEvent.ActorId);
        Assert.Equal(ContactCenterConstants.Components.Voice, declinedEvent.SourceComponent);
        Assert.Equal("q1", declinedEvent.GetData<OfferDeclinedEventData>().QueueId);
        harness.ReservationService.Verify(
            service => service.RejectAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Publisher.Verify(
            publisher => publisher.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.VoiceProviderResolver.Verify(
            resolver => resolver.Get(It.IsAny<string>()),
            Times.Never);
        harness.CallSessionManager.Verify(
            manager => manager.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ProviderCommandStateService.Verify(
            service => service.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ScopeExecutor.Verify(
            executor => executor.ExecuteAsync<IActivityReservationService>(It.IsAny<Func<IActivityReservationService, Task>>()),
            Times.Never);
        harness.ScopeExecutor.Verify(
            executor => executor.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()),
            Times.Never);
    }

    private static void ConfigureAcceptedInboundOffer(Harness harness, List<string> order)
    {
        harness.SetupAcceptedReservation(order);
        harness.SetupInteraction(order);
        harness.SetupNewCallSession(order);
        harness.SetupPublisher(order);
    }

    private static ActivityReservation CreateReservation()
    {
        return new ActivityReservation
        {
            ItemId = "r1",
            AgentId = "a1",
            ActivityItemId = "act1",
            QueueId = "q1",
            Status = ReservationStatus.Pending,
        };
    }

    private static AgentProfile CreateAgentProfile()
    {
        return new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            UserName = "agent",
        };
    }

    private static CallSession CreateCallSession()
    {
        return new CallSession
        {
            ItemId = "session-1",
        };
    }

    private static OmnichannelActivity CreatePreviewDialActivity()
    {
        return new OmnichannelActivity
        {
            ItemId = "act1",
            Source = ActivitySources.PreviewDial,
            CampaignId = "campaign-1",
        };
    }

    private static DialerProfile CreatePreviewDialProfile()
    {
        return new DialerProfile
        {
            ItemId = "profile-1",
            CampaignId = "campaign-1",
            Mode = DialerMode.Preview,
        };
    }

    private static void AssertAnswerRegistration(Harness harness, ProviderCommandRegistration registration)
    {
        Assert.NotNull(registration);
        Assert.Equal(harness.Interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId], registration.CommandId);
        Assert.Equal("dp", registration.ProviderName);
        Assert.Equal(ProviderCommandType.Answer, registration.CommandType);
        Assert.Equal("act1", registration.ActivityItemId);
        Assert.Equal("int1", registration.InteractionId);
        Assert.Equal("r1", registration.ReservationId);
        Assert.False(string.IsNullOrWhiteSpace(registration.RequestPayload));

        var request = JsonSerializer.Deserialize<ProviderAnswerCommandRequest>(registration.RequestPayload);

        Assert.NotNull(request);
        Assert.Equal("act1", request.ActivityId);
        Assert.Equal("int1", request.InteractionId);
        Assert.Equal("call-1", request.ProviderCallId);
        Assert.Equal("a1", request.AgentId);
        Assert.Equal("u1", request.AgentUserId);
        Assert.Equal("q1", request.QueueId);

        var removeReservationProperty = registration.GetType().GetProperty("RemoveReservationFromQueueOnFailure");

        Assert.NotNull(removeReservationProperty);
        Assert.False((bool)removeReservationProperty.GetValue(registration));
    }

    private sealed class Harness
    {
        public Mock<IActivityReservationService> ReservationService { get; } = new();

        public Mock<IActivityReservationManager> ReservationManager { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IOmnichannelActivityManager> ActivityManager { get; } = new();

        public Mock<IDialerProfileManager> DialerProfileManager { get; } = new();

        public Mock<IDialerAttemptService> DialerAttemptService { get; } = new();

        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public Mock<IContactCenterVoiceProviderResolver> VoiceProviderResolver { get; } = new();

        public Mock<ICallSessionManager> CallSessionManager { get; } = new();

        public Mock<IProviderCommandStateService> ProviderCommandStateService { get; } = new();

        public Mock<IContactCenterScopeExecutor> ScopeExecutor { get; } = new();

        public Mock<IContactCenterEventPublisher> Publisher { get; } = new();

        public Mock<IContactCenterVoiceProvider> Provider { get; } = new();

        public Interaction Interaction { get; } = CreateInteraction();

        public CallSession? CreatedCallSession { get; private set; }

        public void SetupPendingReservation()
        {
            ReservationManager
                .Setup(manager => manager.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateReservation());

            AgentManager
                .Setup(manager => manager.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAgentProfile());
        }

        public void SetupAcceptedReservation(List<string>? order = null)
        {
            SetupPendingReservation();

            ReservationService
                .Setup(service => service.AcceptAsync("r1", It.IsAny<CancellationToken>()))
                .Callback(() => order?.Add("accept"))
                .ReturnsAsync(CreateReservation());
        }

        public void SetupInteraction(List<string>? order = null)
        {
            InteractionManager
                .Setup(manager => manager.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Interaction);

            InteractionManager
                .Setup(manager => manager.UpdateAsync(
                    It.IsAny<Interaction>(),
                    It.IsAny<JsonNode>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Interaction, JsonNode, CancellationToken>((_, _, _) => order?.Add("interaction"))
                .Returns(ValueTask.CompletedTask);

            AgentManager
                .Setup(manager => manager.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateAgentProfile());

        }

        public void SetupProvider(VoiceProviderDeliveryModel deliveryModel)
        {
            Provider.SetupGet(provider => provider.TechnicalName).Returns("dp");
            Provider.SetupGet(provider => provider.DeliveryModel).Returns(deliveryModel);

            VoiceProviderResolver
                .Setup(resolver => resolver.Get("dp"))
                .Returns(Provider.Object);
        }

        public void SetupNoProvider()
        {
            VoiceProviderResolver
                .Setup(resolver => resolver.Get("dp"))
                .Returns((IContactCenterVoiceProvider)null!);
        }

        public void SetupPublisher(List<string>? order = null)
        {
            Publisher
                .Setup(publisher => publisher.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => order?.Add(interactionEvent.EventType))
                .Returns(Task.CompletedTask);
        }

        public void SetupNewCallSession(List<string>? order = null)
        {
            CallSessionManager
                .Setup(manager => manager.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((CallSession)null!);

            CallSessionManager
                .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateCallSession());

            CallSessionManager
                .Setup(manager => manager.CreateAsync(It.IsAny<CallSession>(), It.IsAny<CancellationToken>()))
                .Callback<CallSession, CancellationToken>((session, _) =>
                {
                    CreatedCallSession = session;
                    order?.Add("session");
                })
                .Returns(ValueTask.CompletedTask);
        }

        public void SetupPreviewDialAttempt()
        {
            ActivityManager
                .Setup(manager => manager.FindByIdAsync("act1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePreviewDialActivity());

            DialerProfileManager
                .Setup(manager => manager.FindByCampaignAsync("campaign-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreatePreviewDialProfile());

            DialerAttemptService
                .Setup(service => service.TryDialAsync(
                    It.Is<DialerProfile>(profile => profile.ItemId == "profile-1"),
                    It.Is<ActivityReservation>(reservation => reservation.ItemId == "r1"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        public ContactCenterCallCommandService CreateService()
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            return new ContactCenterCallCommandService(
                ReservationService.Object,
                ReservationManager.Object,
                InteractionManager.Object,
                ActivityManager.Object,
                new[] { DialerProfileManager.Object },
                new[] { DialerAttemptService.Object },
                AgentManager.Object,
                VoiceProviderResolver.Object,
                CallSessionManager.Object,
                ProviderCommandStateService.Object,
                ScopeExecutor.Object,
                Publisher.Object,
                clock.Object);
        }
    }

    private static Interaction CreateInteraction()
    {
        return new Interaction
        {
            ItemId = "int1",
            ProviderName = "dp",
            ProviderInteractionId = "call-1",
            Direction = InteractionDirection.Inbound,
        };
    }

}
