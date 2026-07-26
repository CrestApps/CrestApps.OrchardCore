using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies that call control cannot be exercised without passing the authorization boundary.
/// </summary>
/// <remarks>
/// The boundary used to be an optional constructor dependency that callers skipped whenever it was absent from
/// the container. Because the services registering it were not in every feature closure that registered their
/// consumers, a tenant could run the call-control path with the boundary missing, which both skipped the
/// permission check and left the provider call identifier under caller control instead of resolving it from the
/// server-owned call session. These tests pin the boundary as mandatory so the fail-open path cannot return.
/// </remarks>
public sealed class CallControlAuthorizationBoundaryTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 23, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CanDispatchAsync_WhenAuthorizationDenies_DoesNotAllowTheCommand()
    {
        // Arrange
        var interaction = CreateInteraction();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var authorization = FakeCallControlAuthorizationService.Denying();
        var executor = CreateRejectExecutor(interactionManager, authorization);
        var command = CreateCommand();

        // Act
        var canExecute = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canExecute);
        Assert.Single(authorization.Contexts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationDenies_ReturnsARedactedDenialAndNeverReachesTheProvider()
    {
        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var authorization = FakeCallControlAuthorizationService.Denying();
        var executor = CreateRejectExecutor(
            new Mock<IInteractionManager>(MockBehavior.Strict),
            authorization,
            telephonyService);

        var command = CreateCommand();

        // Act
        var result = await executor.ExecuteAsync(command, CreateClaim(command), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("The requested call is not available.", result.ErrorMessage);
        Assert.Single(authorization.Contexts);
        telephonyService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_UsesTheServerResolvedProviderCallId_NotTheOneSuppliedByTheCaller()
    {
        // Arrange
        CallReference capturedCall = null;
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        telephonyService
            .Setup(service => service.RejectAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CallReference call, CancellationToken _) =>
            {
                capturedCall = call;

                return TelephonyResult.Success(new TelephonyCall
                {
                    CallId = call.CallId,
                });
            });

        var authorization = FakeCallControlAuthorizationService.Resolving("server-owned-call");
        var executor = CreateRejectExecutor(
            new Mock<IInteractionManager>(MockBehavior.Loose),
            authorization,
            telephonyService);

        // The caller claims a call identifier it does not own.
        var command = CreateCommand(providerCallId: "attacker-supplied-call");

        // Act
        await executor.ExecuteAsync(command, CreateClaim(command), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedCall);
        Assert.Equal("server-owned-call", capturedCall.CallId);
        Assert.Equal("attacker-supplied-call", Assert.Single(authorization.Contexts).ProviderCallId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCallerIsAnonymous_IsDeniedBeforeAuthorizationIsConsulted()
    {
        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var authorization = new FakeCallControlAuthorizationService();
        var executor = CreateRejectExecutor(
            new Mock<IInteractionManager>(MockBehavior.Strict),
            authorization,
            telephonyService);

        var command = CreateCommand(agentUserId: null);

        // Act
        var result = await executor.ExecuteAsync(command, CreateClaim(command), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(authorization.Contexts);
        telephonyService.VerifyNoOtherCalls();
    }

    private static RejectProviderCommandTypeExecutor CreateRejectExecutor(
        Mock<IInteractionManager> interactionManager,
        ICallControlAuthorizationService authorization,
        Mock<ITelephonyService> telephonyService = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new RejectProviderCommandTypeExecutor(
            [(telephonyService ?? new Mock<ITelephonyService>(MockBehavior.Loose)).Object],
            interactionManager.Object,
            Mock.Of<IActivityQueueService>(),
            Mock.Of<IOmnichannelActivityManager>(),
            Mock.Of<IContactCenterEventPublisher>(),
            clock.Object,
            authorization);
    }

    private static Interaction CreateInteraction()
    {
        return new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            ProviderName = "ProviderA",
            ProviderInteractionId = "call-1",
        };
    }

    private static ProviderCommand CreateCommand(
        string providerCallId = "call-1",
        string agentUserId = "user-1")
    {
        return new ProviderCommand
        {
            CommandId = "command-1",
            CommandType = ProviderCommandType.Reject,
            ProviderName = "ProviderA",
            ActivityItemId = "activity-1",
            InteractionId = "interaction-1",
            RequestPayload = JsonSerializer.Serialize(new ProviderCallActionCommandRequest
            {
                ActivityItemId = "activity-1",
                QueueId = "queue-1",
                ProviderCallId = providerCallId,
                AgentUserId = agentUserId,
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

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task ExecuteAsync_WhenSystemInitiatedCommandHasNoAgentUser_StillReachesTheProvider(
        ProviderCommandType commandType)
    {
        // A call that arrives at a closed entry point or an unroutable queue is terminated by the platform, not
        // by an agent, so its durable payload carries no agent user. Requiring one would silently stop the
        // provider hangup and leave the caller connected to nothing.

        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var reachedProvider = false;
        SetupTelephony(telephonyService, commandType, () =>
        {
            reachedProvider = true;

            return TelephonyResult.Success(new TelephonyCall { CallId = "provider-call-1" });
        });

        var authorization = FakeCallControlAuthorizationService.Resolving("provider-call-1");
        var executor = CreateExecutor(commandType, telephonyService, authorization);
        var command = CreateSystemCommand(commandType);

        // Act
        var result = await executor.ExecuteAsync(command, CreateClaim(command), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(reachedProvider);
        Assert.True(result.Succeeded);

        var context = Assert.Single(authorization.Contexts);
        Assert.Equal(CallControlInitiator.System, context.Initiator);
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task CanDispatchAsync_WhenSystemInitiatedCommandHasNoAgentUser_IsStillDispatchable(
        ProviderCommandType commandType)
    {
        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Loose);
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                Status = InteractionStatus.Ringing,
                ProviderName = "ProviderA",
                ProviderInteractionId = "provider-call-1",
            });

        var authorization = FakeCallControlAuthorizationService.Resolving("provider-call-1");
        var executor = CreateExecutor(commandType, telephonyService, authorization, interactionManager);
        var command = CreateSystemCommand(commandType);

        // Act
        var canDispatch = await executor.CanDispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(canDispatch);
    }

    [Theory]
    [InlineData(ProviderCommandType.Reject)]
    [InlineData(ProviderCommandType.SendToVoicemail)]
    public async Task ExecuteAsync_WhenSystemInitiatedCommandIsDenied_NeverReachesTheProvider(
        ProviderCommandType commandType)
    {
        // The system initiator relaxes the ownership check, not the boundary itself.

        // Arrange
        var telephonyService = new Mock<ITelephonyService>(MockBehavior.Strict);
        var authorization = FakeCallControlAuthorizationService.Denying();
        var executor = CreateExecutor(commandType, telephonyService, authorization);
        var command = CreateSystemCommand(commandType);

        // Act
        var result = await executor.ExecuteAsync(command, CreateClaim(command), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        telephonyService.VerifyNoOtherCalls();
    }

    private static ProviderCallActionCommandTypeExecutor CreateExecutor(
        ProviderCommandType commandType,
        Mock<ITelephonyService> telephonyService,
        ICallControlAuthorizationService authorization,
        Mock<IInteractionManager> interactionManager = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        interactionManager ??= new Mock<IInteractionManager>(MockBehavior.Loose);

        return commandType switch
        {
            ProviderCommandType.Reject => new RejectProviderCommandTypeExecutor(
                [telephonyService.Object],
                interactionManager.Object,
                Mock.Of<IActivityQueueService>(),
                Mock.Of<IOmnichannelActivityManager>(),
                Mock.Of<IContactCenterEventPublisher>(),
                clock.Object,
                authorization),
            ProviderCommandType.SendToVoicemail => new SendToVoicemailProviderCommandTypeExecutor(
                [telephonyService.Object],
                interactionManager.Object,
                Mock.Of<IActivityQueueService>(),
                Mock.Of<IOmnichannelActivityManager>(),
                Mock.Of<IContactCenterEventPublisher>(),
                clock.Object,
                authorization),
            _ => throw new ArgumentOutOfRangeException(nameof(commandType)),
        };
    }

    private static void SetupTelephony(
        Mock<ITelephonyService> telephonyService,
        ProviderCommandType commandType,
        Func<TelephonyResult> result)
    {
        if (commandType == ProviderCommandType.Reject)
        {
            telephonyService
                .Setup(service => service.RejectAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            return;
        }

        telephonyService
            .Setup(service => service.SendToVoicemailAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private static ProviderCommand CreateSystemCommand(ProviderCommandType commandType)
    {
        return new ProviderCommand
        {
            CommandId = "command-1",
            CommandType = commandType,
            ProviderName = "ProviderA",
            ActivityItemId = "activity-1",
            InteractionId = "interaction-1",
            RequestPayload = JsonSerializer.Serialize(new ProviderCallActionCommandRequest
            {
                Initiator = CallControlInitiator.System,
                ActivityItemId = "activity-1",
                InteractionId = "interaction-1",
                ProviderCallId = "provider-call-1",
                Metadata = new Dictionary<string, object>(),
            }),
        };
    }
}
