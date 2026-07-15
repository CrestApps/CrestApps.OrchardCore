#nullable enable annotations

using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderCallActionCommandTypeExecutorTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 23, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task ExecuteAsync_WhenTelephonyServiceSucceeds_MapsTelephonyResultAndPassesNormalizedMetadata(
        ProviderCommandType commandType)
    {
        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        CallReference? capturedCall = null;

        SetupTelephonySuccess(telephonyService, commandType, call =>
        {
            capturedCall = call;
            return TelephonyResult.Success(new TelephonyCall
            {
                CallId = "provider-call-77",
            });
        });

        var executor = CreateExecutor(
            commandType,
            telephonyService,
            new Mock<IInteractionManager>(MockBehavior.Strict),
            new Mock<IContactCenterEventPublisher>(MockBehavior.Strict),
            CreateClock());

        var command = CreateCommand(
            commandType,
            requestMetadata: new Dictionary<string, object>
            {
                ["queueId"] = "queue-1",
                ["voicemailRecipientUserId"] = "user-1",
            });

        // Act
        var result = await executor.ExecuteAsync(command, CreateClaim(command), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCall);
        Assert.Equal("call-1", capturedCall!.CallId);
        Assert.Equal("queue-1", capturedCall.Metadata["queueId"]);
        Assert.Equal("user-1", capturedCall.Metadata["voicemailRecipientUserId"]);
        Assert.Equal("command-1", capturedCall.Metadata[ContactCenterConstants.CommandMetadata.CommandId]);
        Assert.Equal("1", capturedCall.Metadata[ContactCenterConstants.CommandMetadata.FenceToken]);
        Assert.Equal("worker-1", capturedCall.Metadata["providerCommandOwner"]);
        Assert.True(result.Succeeded);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal("call-1", result.ProviderCallId);
        Assert.Equal("ProviderA", result.ProviderName);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task ExecuteAsync_WhenTelephonyServiceCannotProveOutcome_MapsUnknownProviderResult(
        ProviderCommandType commandType)
    {
        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        SetupTelephonyUnknown(telephonyService, commandType);

        var executor = CreateExecutor(
            commandType,
            telephonyService,
            new Mock<IInteractionManager>(MockBehavior.Strict),
            new Mock<IContactCenterEventPublisher>(MockBehavior.Strict),
            CreateClock());

        var command = CreateCommand(commandType);

        // Act
        var result = await executor.ExecuteAsync(command, CreateClaim(command), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("call-1", result.ProviderCallId);
        Assert.Equal("ProviderA", result.ProviderName);
        Assert.Equal($"{ActionPrefix(commandType)}_outcome_unknown", result.ErrorCode);
        Assert.Equal("The provider could not prove the outcome.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task CanDispatchAsync_WhenRequestPayloadIsInvalid_ReturnsFalse(
        ProviderCommandType commandType)
    {
        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var executor = CreateExecutor(
            commandType,
            telephonyService,
            new Mock<IInteractionManager>(MockBehavior.Strict),
            new Mock<IContactCenterEventPublisher>(MockBehavior.Strict),
            CreateClock());

        var command = CreateCommand(commandType, requestPayload: "{");

        // Act
        var canDispatch = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canDispatch);
        telephonyService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task CanDispatchAsync_WhenInteractionIsTerminal_ReturnsFalse(
        ProviderCommandType commandType)
    {
        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                ProviderInteractionId = "call-1",
                Status = InteractionStatus.Ended,
            });

        var executor = CreateExecutor(
            commandType,
            telephonyService,
            interactionManager,
            new Mock<IContactCenterEventPublisher>(MockBehavior.Strict),
            CreateClock());

        var command = CreateCommand(commandType);

        // Act
        var canDispatch = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canDispatch);
        interactionManager.Verify(
            manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()),
            Times.Once);
        telephonyService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task CanDispatchAsync_WhenProviderCallIdDoesNotMatchInteraction_ReturnsFalse(
        ProviderCommandType commandType)
    {
        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                ProviderInteractionId = "call-2",
                Status = InteractionStatus.Ringing,
            });

        var executor = CreateExecutor(
            commandType,
            telephonyService,
            interactionManager,
            new Mock<IContactCenterEventPublisher>(MockBehavior.Strict),
            CreateClock());

        var command = CreateCommand(commandType);

        // Act
        var canDispatch = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canDispatch);
        interactionManager.Verify(
            manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()),
            Times.Once);
        telephonyService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task ProjectSuccessAsync_WhenTelephonyActionSucceeds_MarksInteractionEndedAndPublishesCallEnded(
        ProviderCommandType commandType)
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Ringing,
        };
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var publishedEvents = new List<InteractionEvent>();
        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => publishedEvents.Add(interactionEvent))
            .Returns(Task.CompletedTask);

        var executor = CreateExecutor(
            commandType,
            new Mock<ITelephonyService>(MockBehavior.Strict),
            interactionManager,
            publisher,
            CreateClock());

        var command = CreateCommand(commandType);
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "provider-call-77",
            ProviderName = "ProviderA",
        };

        // Act
        await executor.ProjectSuccessAsync(command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ended, interaction.Status);
        Assert.Equal(_now, interaction.EndedUtc);
        Assert.Equal("Succeeded", interaction.TechnicalMetadata["providerCallActionOutcome"]);
        Assert.Equal(command.CommandId, interaction.TechnicalMetadata["providerCallActionCommandId"]);
        Assert.Equal(commandType.ToString(), interaction.TechnicalMetadata["providerCallActionType"]);
        interactionManager.Verify(
            manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Single(publishedEvents);
        Assert.Equal(ContactCenterConstants.Events.CallEnded, publishedEvents[0].EventType);
        Assert.Equal("interaction-1", publishedEvents[0].InteractionId);
        Assert.Equal(command.CommandId, publishedEvents[0].IdempotencyKey);
        Assert.False(string.IsNullOrWhiteSpace(publishedEvents[0].Data));
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task ProjectFailureAsync_WhenTelephonyActionFails_WritesDiagnosticProjectionWithoutPublishing(
        ProviderCommandType commandType)
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Ringing,
        };
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        var executor = CreateExecutor(
            commandType,
            new Mock<ITelephonyService>(MockBehavior.Strict),
            interactionManager,
            publisher,
            CreateClock());

        var command = CreateCommand(commandType);

        // Act
        await executor.ProjectFailureAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Null(interaction.EndedUtc);
        Assert.Equal("Failed", interaction.TechnicalMetadata["providerCallActionOutcome"]);
        Assert.Equal($"{ActionPrefix(commandType)}_failed", interaction.TechnicalMetadata["providerCallActionErrorCode"]);
        Assert.Equal(commandType.ToString(), interaction.TechnicalMetadata["providerCallActionType"]);
        interactionManager.Verify(
            manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publisher.Verify(
            value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task ProjectFailureAsync_WhenTimeoutActionRequiresReoffer_RestoresLiveWorkAndPublishesRequeue(
        ProviderCommandType commandType)
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Ringing,
            EndedUtc = _now,
        };
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var queueService = new Mock<IActivityQueueService>(MockBehavior.Strict);
        queueService
            .Setup(service => service.EnqueueAsync(
                "activity-1",
                "queue-1",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem());
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            AssignmentStatus = ActivityAssignmentStatus.Reserved,
            Status = ActivityStatus.Completed,
            CompletedUtc = _now,
        };
        var activityManager = new Mock<IOmnichannelActivityManager>(MockBehavior.Strict);
        activityManager
            .Setup(manager => manager.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        activityManager
            .Setup(manager => manager.UpdateAsync(
                activity,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        InteractionEvent publishedEvent = null;
        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => publishedEvent = interactionEvent)
            .Returns(Task.CompletedTask);
        var executor = CreateExecutor(
            commandType,
            new Mock<ITelephonyService>(MockBehavior.Strict),
            interactionManager,
            publisher,
            CreateClock(),
            queueService,
            activityManager);
        var command = CreateCommand(
            commandType,
            requestPayload: JsonSerializer.Serialize(new ProviderCallActionCommandRequest
            {
                ActivityItemId = "activity-1",
                QueueId = "queue-1",
                ProviderCallId = "call-1",
                ReofferOnFailure = true,
            }));

        // Act
        await executor.ProjectFailureAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.EndedUtc);
        Assert.Equal("FailedRequeued", interaction.TechnicalMetadata["providerCallActionOutcome"]);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.AwaitingAgentResponse, activity.Status);
        Assert.Null(activity.CompletedUtc);
        Assert.NotNull(publishedEvent);
        Assert.Equal(ContactCenterConstants.Events.OfferRequeued, publishedEvent.EventType);
        Assert.StartsWith("provider-domain-event:v1:", publishedEvent.IdempotencyKey, StringComparison.Ordinal);
        queueService.VerifyAll();
        activityManager.VerifyAll();
        interactionManager.VerifyAll();
        publisher.VerifyAll();
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task ProjectOutcomeUnknownAsync_WhenTelephonyOutcomeIsUnknown_WritesDiagnosticProjectionWithoutPublishing(
        ProviderCommandType commandType)
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ProviderInteractionId = "call-1",
            Status = InteractionStatus.Ringing,
        };
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        interactionManager
            .Setup(manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var publisher = new Mock<IContactCenterEventPublisher>(MockBehavior.Strict);
        var executor = CreateExecutor(
            commandType,
            new Mock<ITelephonyService>(MockBehavior.Strict),
            interactionManager,
            publisher,
            CreateClock());

        var command = CreateCommand(commandType);

        // Act
        await executor.ProjectOutcomeUnknownAsync(command, "provider_outcome_unknown", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
        Assert.Null(interaction.EndedUtc);
        Assert.Equal("Unknown", interaction.TechnicalMetadata["providerCallActionOutcome"]);
        Assert.Equal("provider_outcome_unknown", interaction.TechnicalMetadata["providerCallActionErrorCode"]);
        Assert.Equal(commandType.ToString(), interaction.TechnicalMetadata["providerCallActionType"]);
        interactionManager.Verify(
            manager => manager.UpdateAsync(
                interaction,
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publisher.Verify(
            value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ProviderCommand CreateCommand(
        ProviderCommandType commandType,
        Dictionary<string, object>? requestMetadata = null,
        string? requestPayload = null,
        string interactionId = "interaction-1")
    {
        requestMetadata ??= new Dictionary<string, object>
        {
            ["queueId"] = "queue-1",
            ["voicemailRecipientUserId"] = "user-1",
        };

        return new ProviderCommand
        {
            CommandId = "command-1",
            CommandType = commandType,
            ProviderName = "ProviderA",
            ActivityItemId = "activity-1",
            InteractionId = interactionId,
            RequestPayload = requestPayload ?? JsonSerializer.Serialize(new ProviderCallActionCommandRequest
            {
                ActivityItemId = "activity-1",
                QueueId = "queue-1",
                ProviderCallId = "call-1",
                Metadata = requestMetadata,
            }),
        };
    }

    private static ProviderCommandClaim CreateClaim(ProviderCommand command)
    {
        return new ProviderCommandClaim
        {
            CommandId = command.CommandId,
            FenceToken = 1,
            OwnerToken = "worker-1",
            LeaseExpiresUtc = _now.AddMinutes(5),
        };
    }

    private static Mock<IClock> CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return clock;
    }

    private static ProviderCallActionCommandTypeExecutor CreateExecutor(
        ProviderCommandType commandType,
        Mock<ITelephonyService> telephonyService,
        Mock<IInteractionManager> interactionManager,
        Mock<IContactCenterEventPublisher> publisher,
        Mock<IClock> clock,
        Mock<IActivityQueueService>? queueService = null,
        Mock<IOmnichannelActivityManager>? activityManager = null)
    {
        queueService ??= new Mock<IActivityQueueService>(MockBehavior.Loose);
        activityManager ??= new Mock<IOmnichannelActivityManager>(MockBehavior.Loose);

        return commandType switch
        {
            ProviderCommandType.Reject => new RejectProviderCommandTypeExecutor(
                [telephonyService.Object],
                interactionManager.Object,
                queueService.Object,
                activityManager.Object,
                publisher.Object,
                clock.Object),
            ProviderCommandType.SendToVoicemail => new SendToVoicemailProviderCommandTypeExecutor(
                [telephonyService.Object],
                interactionManager.Object,
                queueService.Object,
                activityManager.Object,
                publisher.Object,
                clock.Object),
            _ => throw new ArgumentOutOfRangeException(nameof(commandType)),
        };
    }

    private static void SetupTelephonySuccess(
        Mock<ITelephonyService> telephonyService,
        ProviderCommandType commandType,
        Func<CallReference, TelephonyResult> resultFactory)
    {
        switch (commandType)
        {
            case ProviderCommandType.Reject:
                telephonyService
                    .Setup(service => service.RejectAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
                    .Returns<CallReference, CancellationToken>((call, _) => Task.FromResult(resultFactory(call)));
                break;
            case ProviderCommandType.SendToVoicemail:
                telephonyService
                    .Setup(service => service.SendToVoicemailAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
                    .Returns<CallReference, CancellationToken>((call, _) => Task.FromResult(resultFactory(call)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(commandType));
        }
    }

    private static void SetupTelephonyUnknown(
        Mock<ITelephonyService> telephonyService,
        ProviderCommandType commandType)
    {
        switch (commandType)
        {
            case ProviderCommandType.Reject:
                telephonyService
                    .Setup(service => service.RejectAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new TelephonyResult
                    {
                        OutcomeUnknown = true,
                        Error = "The provider could not prove the outcome.",
                        Call = new TelephonyCall
                        {
                            CallId = "provider-call-77",
                        },
                    });
                break;
            case ProviderCommandType.SendToVoicemail:
                telephonyService
                    .Setup(service => service.SendToVoicemailAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new TelephonyResult
                    {
                        OutcomeUnknown = true,
                        Error = "The provider could not prove the outcome.",
                        Call = new TelephonyCall
                        {
                            CallId = "provider-call-77",
                        },
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(commandType));
        }
    }

    private static string ActionPrefix(ProviderCommandType commandType)
    {
        return commandType switch
        {
            ProviderCommandType.Reject => "reject",
            ProviderCommandType.SendToVoicemail => "voicemail",
            _ => throw new ArgumentOutOfRangeException(nameof(commandType)),
        };
    }
}
