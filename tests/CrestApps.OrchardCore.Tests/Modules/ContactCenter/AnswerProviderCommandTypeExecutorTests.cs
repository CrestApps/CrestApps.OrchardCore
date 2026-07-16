#nullable enable annotations

using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AnswerProviderCommandTypeExecutorTests
{
    private const string OwnerMetadataKey = "providerCommandOwner";
    private const string ProviderErrorCodeKey = "providerErrorCode";

    private static readonly DateTime _now = new(2026, 7, 15, 19, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CanDispatchAsync_WhenPayloadIsInvalid_ReturnsFalse()
    {
        // Arrange
        var harness = new Harness();
        var executor = harness.CreateExecutor();
        var command = CreateCommandFromPayload("{not-json}");

        // Act
        var canDispatch = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canDispatch);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenRequiredIdentifiersAreMissing_ReturnsFalse()
    {
        // Arrange
        var harness = new Harness();
        var executor = harness.CreateExecutor();
        var command = CreateCommand(new ProviderAnswerCommandRequest
        {
            ActivityId = "activity-1",
            InteractionId = "interaction-1",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            AgentUserId = string.Empty,
            QueueId = "queue-1",
        });

        // Act
        var canDispatch = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canDispatch);
    }

    [Theory]
    [InlineData(InteractionStatus.Ended, ContactCenterCallState.Ringing)]
    [InlineData(InteractionStatus.Ringing, ContactCenterCallState.Ended)]
    public async Task CanDispatchAsync_WhenInteractionOrCallSessionIsTerminal_ReturnsFalse(
        InteractionStatus interactionStatus,
        ContactCenterCallState sessionState)
    {
        // Arrange
        var harness = new Harness();
        harness.SetupDispatchableState(interactionStatus, sessionState);
        var executor = harness.CreateExecutor();
        var command = CreateCommand();

        // Act
        var canDispatch = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canDispatch);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenProviderCallHasChanged_ReturnsFalse()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupDispatchableState(InteractionStatus.Ringing, ContactCenterCallState.Ringing);
        harness.Interaction.ProviderInteractionId = "call-2";
        var executor = harness.CreateExecutor();
        var command = CreateCommand();

        // Act
        var canDispatch = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canDispatch);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderExists_UsesProviderConnectAndStampsMetadata()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupProviderResult(new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = string.Empty,
        });
        var executor = harness.CreateExecutor();
        var command = CreateCommand();
        var claim = CreateClaim();

        // Act
        var result = await executor.ExecuteAsync(command, claim, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("provider-a", result.ProviderName);
        Assert.Equal("call-1", result.ProviderCallId);
        Assert.NotNull(harness.LastConnectRequest);
        Assert.Null(harness.LastCallReference);
        Assert.Equal("activity-1", harness.LastConnectRequest!.ActivityId);
        Assert.Equal("interaction-1", harness.LastConnectRequest.InteractionId);
        Assert.Equal("call-1", harness.LastConnectRequest.ProviderCallId);
        Assert.Equal("agent-1", harness.LastConnectRequest.AgentId);
        Assert.Equal("user-1", harness.LastConnectRequest.AgentUserId);
        Assert.Equal("queue-1", harness.LastConnectRequest.QueueId);
        Assert.Equal("command-1", harness.LastConnectRequest.Metadata[ContactCenterConstants.CommandMetadata.CommandId]);
        Assert.Equal("7", harness.LastConnectRequest.Metadata[ContactCenterConstants.CommandMetadata.FenceToken]);
        Assert.Equal("owner-1", harness.LastConnectRequest.Metadata[OwnerMetadataKey]);
        harness.TelephonyService.Verify(
            service => service.AnswerAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderIsMissing_UsesTelephonyFallbackAndConvertsResult()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupNoProvider();
        harness.SetupTelephonyResult(TelephonyResult.Success());
        var executor = harness.CreateExecutor();
        var command = CreateCommand();
        var claim = CreateClaim();

        // Act
        var result = await executor.ExecuteAsync(command, claim, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("provider-a", result.ProviderName);
        Assert.Equal("call-1", result.ProviderCallId);
        Assert.NotNull(harness.LastCallReference);
        Assert.Null(harness.LastConnectRequest);
        Assert.Equal("call-1", harness.LastCallReference!.CallId);
        Assert.Equal("command-1", harness.LastCallReference.Metadata[ContactCenterConstants.CommandMetadata.CommandId]);
        Assert.Equal("7", harness.LastCallReference.Metadata[ContactCenterConstants.CommandMetadata.FenceToken]);
        Assert.Equal("owner-1", harness.LastCallReference.Metadata[OwnerMetadataKey]);
        harness.CallControlProvider.Verify(
            provider => provider.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenAnswerSucceeds_UpdatesInteractionSessionAndPublishesCallConnected()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupActiveState();
        harness.SetupPublisher();
        var executor = harness.CreateExecutor();
        var command = CreateCommand();
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderName = "provider-a",
            ProviderCallId = "call-1",
        };

        // Act
        await executor.ProjectSuccessAsync(command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Connected, harness.Interaction.Status);
        Assert.Equal(_now, harness.Interaction.StartedUtc);
        Assert.Equal(_now, harness.Interaction.AnsweredUtc);
        Assert.Equal("agent-1", harness.Interaction.AgentId);
        Assert.Equal("queue-1", harness.Interaction.QueueId);
        Assert.Equal(ContactCenterCallState.Connected, harness.Session.State);
        Assert.Equal(_now, harness.Session.StartedUtc);
        Assert.Equal(_now, harness.Session.AnsweredUtc);
        Assert.Equal("agent-1", harness.Session.AgentId);
        Assert.Equal("queue-1", harness.Session.QueueId);
        Assert.Single(harness.PublishedEvents);
        var publishedEvent = harness.PublishedEvents[0];
        Assert.Equal(ContactCenterConstants.Events.CallConnected, publishedEvent.EventType);
        Assert.Equal(nameof(Interaction), publishedEvent.AggregateType);
        Assert.Equal("interaction-1", publishedEvent.AggregateId);
        Assert.Equal("agent-1", publishedEvent.ActorId);
        Assert.Equal(ContactCenterConstants.Components.Voice, publishedEvent.SourceComponent);
        Assert.Equal(ContactCenterClaimKeys.BuildProviderDomainEventIdempotencyKey(command.CommandId, ContactCenterConstants.Events.CallConnected), publishedEvent.IdempotencyKey);
    }

    [Fact]
    public async Task ProjectFailureAsync_WhenReofferOnFailureIsTrue_PreservesCallAndPublishesOfferRequeued()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupActiveState();
        harness.SetupPublisher();
        var executor = harness.CreateExecutor();
        var command = CreateCommand(reofferOnFailure: true);

        // Act
        await executor.ProjectFailureAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ringing, harness.Interaction.Status);
        Assert.Null(harness.Interaction.EndedUtc);
        Assert.Equal(ContactCenterCallState.Ringing, harness.Session.State);
        Assert.Null(harness.Session.EndedUtc);
        Assert.Single(harness.PublishedEvents);
        Assert.Equal(ContactCenterConstants.Events.OfferRequeued, harness.PublishedEvents[0].EventType);
        Assert.Equal(nameof(ActivityReservation), harness.PublishedEvents[0].AggregateType);
        Assert.Equal("reservation-1", harness.PublishedEvents[0].AggregateId);
        Assert.Equal("queue-1", harness.PublishedEvents[0].GetData<OfferDeclinedEventData>().QueueId);
        Assert.Equal(ContactCenterClaimKeys.BuildProviderDomainEventIdempotencyKey(command.CommandId, ContactCenterConstants.Events.OfferRequeued), harness.PublishedEvents[0].IdempotencyKey);
    }

    [Fact]
    public async Task ProjectFailureAsync_WhenReofferOnFailureIsFalse_PublishesCallEndedOnly()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupActiveState();
        harness.SetupPublisher();
        var executor = harness.CreateExecutor();
        var command = CreateCommand();

        // Act
        await executor.ProjectFailureAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Failed, harness.Interaction.Status);
        Assert.Equal(_now, harness.Interaction.EndedUtc);
        Assert.Equal(ContactCenterCallState.Ended, harness.Session.State);
        Assert.Equal(_now, harness.Session.EndedUtc);
        Assert.Single(harness.PublishedEvents);
        Assert.Equal(ContactCenterConstants.Events.CallEnded, harness.PublishedEvents[0].EventType);
        Assert.Equal(nameof(Interaction), harness.PublishedEvents[0].AggregateType);
        Assert.Equal("interaction-1", harness.PublishedEvents[0].AggregateId);
    }

    [Fact]
    public async Task ProjectOutcomeUnknownAsync_WhenOutcomeIsUnknown_PreservesRingingAndRecordsDiagnostics()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupActiveState();
        var executor = harness.CreateExecutor();
        var command = CreateCommand();

        // Act
        await executor.ProjectOutcomeUnknownAsync(command, "provider-unknown", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ringing, harness.Interaction.Status);
        Assert.Equal(ContactCenterCallState.Ringing, harness.Session.State);
        Assert.Equal("provider-unknown", harness.Interaction.TechnicalMetadata[ProviderErrorCodeKey]);
        Assert.Equal("provider-unknown", harness.Session.Metadata[ProviderErrorCodeKey]);
        Assert.Empty(harness.PublishedEvents);
    }

    private static ProviderCommand CreateCommandFromPayload(string requestPayload)
    {
        return CreateCommand(null, requestPayload);
    }

    private static ProviderCommand CreateCommand(
        ProviderAnswerCommandRequest? request = null,
        string? requestPayload = null,
        bool reofferOnFailure = false)
    {
        request ??= CreateRequest(reofferOnFailure);
        requestPayload ??= JsonSerializer.Serialize(request);

        return new ProviderCommand
        {
            CommandId = "command-1",
            ProviderName = "provider-a",
            CommandType = ProviderCommandType.Answer,
            ActivityItemId = request.ActivityId,
            InteractionId = request.InteractionId,
            ReservationId = "reservation-1",
            RequestPayload = requestPayload,
        };
    }

    private static ProviderAnswerCommandRequest CreateRequest(bool reofferOnFailure = false)
    {
        return new ProviderAnswerCommandRequest
        {
            ActivityId = "activity-1",
            InteractionId = "interaction-1",
            ProviderCallId = "call-1",
            AgentId = "agent-1",
            AgentUserId = "user-1",
            QueueId = "queue-1",
            ReofferOnFailure = reofferOnFailure,
        };
    }

    private static ProviderCommandClaim CreateClaim()
    {
        return new ProviderCommandClaim
        {
            CommandId = "command-1",
            FenceToken = 7,
            OwnerToken = "owner-1",
        };
    }

    private sealed class Harness
    {
        public Harness()
        {
            CallControlProvider = Provider.As<IContactCenterVoiceCallControlProvider>();
        }

        public Mock<IContactCenterVoiceProviderResolver> VoiceProviderResolver { get; } = new(MockBehavior.Strict);

        public Mock<ITelephonyService> TelephonyService { get; } = new(MockBehavior.Strict);

        public Mock<IInteractionManager> InteractionManager { get; } = new(MockBehavior.Strict);

        public Mock<ICallSessionManager> CallSessionManager { get; } = new(MockBehavior.Strict);

        public Mock<IContactCenterEventPublisher> Publisher { get; } = new(MockBehavior.Strict);

        public Mock<IContactCenterVoiceProvider> Provider { get; } = new(MockBehavior.Strict);

        public Mock<IContactCenterVoiceCallControlProvider> CallControlProvider { get; }

        public Interaction Interaction { get; } = CreateInteraction();

        public CallSession Session { get; } = CreateSession();

        public ContactCenterConnectRequest? LastConnectRequest { get; private set; }

        public CallReference? LastCallReference { get; private set; }

        public List<InteractionEvent> PublishedEvents { get; } = [];

        public AnswerProviderCommandTypeExecutor CreateExecutor()
        {
            var clock = new Mock<IClock>(MockBehavior.Strict);
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            return new AnswerProviderCommandTypeExecutor(
                VoiceProviderResolver.Object,
                TelephonyService.Object,
                InteractionManager.Object,
                CallSessionManager.Object,
                Publisher.Object,
                clock.Object);
        }

        public void SetupActiveState()
        {
            Interaction.Status = InteractionStatus.Ringing;
            Interaction.StartedUtc = null;
            Interaction.AnsweredUtc = null;
            Interaction.EndedUtc = null;
            Interaction.AgentId = "agent-1";
            Interaction.QueueId = "queue-1";
            Interaction.ProviderName = "provider-a";
            Interaction.ProviderInteractionId = "call-1";
            Interaction.TechnicalMetadata.Clear();

            Session.State = ContactCenterCallState.Ringing;
            Session.StartedUtc = null;
            Session.AnsweredUtc = null;
            Session.EndedUtc = null;
            Session.AgentId = "agent-1";
            Session.QueueId = "queue-1";
            Session.ProviderName = "provider-a";
            Session.ProviderCallId = "call-1";
            Session.Metadata.Clear();

            InteractionManager
                .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Interaction);

            InteractionManager
                .Setup(manager => manager.UpdateAsync(Interaction, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            CallSessionManager
                .Setup(manager => manager.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Session);

            CallSessionManager
                .Setup(manager => manager.UpdateAsync(Session, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
        }

        public void SetupDispatchableState(InteractionStatus interactionStatus, ContactCenterCallState sessionState)
        {
            SetupActiveState();
            Interaction.Status = interactionStatus;
            Session.State = sessionState;
        }

        public void SetupProviderResult(ContactCenterVoiceProviderResult result)
        {
            Provider
                .SetupGet(provider => provider.TechnicalName)
                .Returns("provider-a");

            CallControlProvider
                .Setup(provider => provider.ConnectToAgentAsync(It.IsAny<ContactCenterConnectRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ContactCenterConnectRequest, CancellationToken>((request, _) => LastConnectRequest = request)
                .ReturnsAsync(result);

            VoiceProviderResolver
                .Setup(resolver => resolver.Get("provider-a"))
                .Returns(Provider.Object);
        }

        public void SetupNoProvider()
        {
            VoiceProviderResolver
                .Setup(resolver => resolver.Get("provider-a"))
                .Returns((IContactCenterVoiceProvider)null!);
        }

        public void SetupTelephonyResult(TelephonyResult result)
        {
            TelephonyService
                .Setup(service => service.AnswerAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
                .Callback<CallReference, CancellationToken>((request, _) => LastCallReference = request)
                .ReturnsAsync(result);
        }

        public void SetupPublisher()
        {
            Publisher
                .Setup(publisher => publisher.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => PublishedEvents.Add(interactionEvent))
                .Returns(Task.CompletedTask);
        }

        private static Interaction CreateInteraction()
        {
            return new Interaction
            {
                ItemId = "interaction-1",
                ProviderName = "provider-a",
                ProviderInteractionId = "call-1",
                Direction = InteractionDirection.Inbound,
            };
        }

        private static CallSession CreateSession()
        {
            return new CallSession
            {
                ItemId = "session-1",
                ProviderName = "provider-a",
                ProviderCallId = "call-1",
                Direction = InteractionDirection.Inbound,
            };
        }
    }
}
