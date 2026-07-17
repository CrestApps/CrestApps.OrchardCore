using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialProviderCommandTypeExecutorTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 23, 0, 0, DateTimeKind.Utc);

    // CanDispatchAsync

    [Fact]
    public async Task CanDispatchAsync_WhenPayloadIsNull_ReturnsFalse()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Command.RequestPayload = null;

        // Act
        var result = await harness.Executor.CanDispatchAsync(harness.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        harness.Validator.Verify(
            v => v.CanDispatchAsync(It.IsAny<ProviderCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenPayloadIsWhitespace_ReturnsFalse()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Command.RequestPayload = "   ";

        // Act
        var result = await harness.Executor.CanDispatchAsync(harness.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenNoValidatorsRegistered_ReturnsFalse()
    {
        // Arrange
        var harness = CreateHarness(validators: []);

        // Act
        var result = await harness.Executor.CanDispatchAsync(harness.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenValidatorApproves_ReturnsTrue()
    {
        // Arrange
        var harness = CreateHarness(canDispatch: true);

        // Act
        var result = await harness.Executor.CanDispatchAsync(harness.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanDispatchAsync_WhenValidatorDeclines_ReturnsFalse()
    {
        // Arrange
        var harness = CreateHarness(canDispatch: false);

        // Act
        var result = await harness.Executor.CanDispatchAsync(harness.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    // ExecuteAsync

    [Fact]
    public async Task ExecuteAsync_StampsRequestWithCommandIdAndFenceToken()
    {
        // Arrange
        var harness = CreateHarness();
        ContactCenterDialRequest capturedRequest = null;
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<ContactCenterDialRequest, string, CancellationToken>((req, _, _) => capturedRequest = req)
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderCallId = "call-1" });

        // Act
        await harness.Executor.ExecuteAsync(harness.Command, harness.Claim, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("command-1", capturedRequest.CommandId);
        Assert.Equal("command-1", capturedRequest.Metadata[ContactCenterConstants.CommandMetadata.CommandId]);
        Assert.Equal("command-1", capturedRequest.Metadata[TelephonyConstants.RequestMetadata.IdempotencyKey]);
        Assert.Equal("1", capturedRequest.Metadata[ContactCenterConstants.CommandMetadata.FenceToken]);
        Assert.Equal("1", capturedRequest.Metadata[TelephonyConstants.RequestMetadata.FenceToken]);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRouterWithProviderNameFromCommand()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true, ProviderCallId = "call-1" });

        // Act
        await harness.Executor.ExecuteAsync(harness.Command, harness.Claim, TestContext.Current.CancellationToken);

        // Assert
        harness.Router.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRouterResult()
    {
        // Arrange
        var harness = CreateHarness();
        var expected = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-xyz",
            ProviderName = "my-provider",
        };
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await harness.Executor.ExecuteAsync(harness.Command, harness.Claim, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expected, result);
    }

    // ProjectSuccessAsync

    [Fact]
    public async Task ProjectSuccessAsync_SetsInteractionStatusToRinging()
    {
        // Arrange
        var harness = CreateHarness();
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-1",
            ProviderName = "provider",
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ringing, harness.Interaction.Status);
    }

    [Fact]
    public async Task ProjectSuccessAsync_SetsInteractionProviderInteractionId()
    {
        // Arrange
        var harness = CreateHarness();
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-99",
            ProviderName = "provider",
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("call-99", harness.Interaction.ProviderInteractionId);
    }

    [Fact]
    public async Task ProjectSuccessAsync_SetsInteractionProviderNameFromResult()
    {
        // Arrange
        var harness = CreateHarness();
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-1",
            ProviderName = "result-provider",
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("result-provider", harness.Interaction.ProviderName);
    }

    [Fact]
    public async Task ProjectSuccessAsync_SetsInteractionProviderNameFromCommandWhenResultProviderNameIsEmpty()
    {
        // Arrange
        var harness = CreateHarness();
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-1",
            ProviderName = string.Empty,
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("provider", harness.Interaction.ProviderName);
    }

    [Fact]
    public async Task ProjectSuccessAsync_SetsInteractionStartedUtc()
    {
        // Arrange
        var harness = CreateHarness();
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-1",
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now, harness.Interaction.StartedUtc);
    }

    [Fact]
    public async Task ProjectSuccessAsync_SetsActivityStatusToDialing()
    {
        // Arrange
        var harness = CreateHarness();
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-1",
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityStatus.Dialing, harness.Activity.Status);
    }

    // ProjectFailureAsync

    [Fact]
    public async Task ProjectFailureAsync_SetsInteractionStatusToFailed()
    {
        // Arrange
        var harness = CreateHarness();

        // Act
        await harness.Executor.ProjectFailureAsync(harness.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Failed, harness.Interaction.Status);
    }

    [Fact]
    public async Task ProjectFailureAsync_SetsInteractionEndedUtc()
    {
        // Arrange
        var harness = CreateHarness();

        // Act
        await harness.Executor.ProjectFailureAsync(harness.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now, harness.Interaction.EndedUtc);
    }

    [Fact]
    public async Task ProjectFailureAsync_SetsActivityStatusToFailed()
    {
        // Arrange
        var harness = CreateHarness();

        // Act
        await harness.Executor.ProjectFailureAsync(harness.Command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityStatus.Failed, harness.Activity.Status);
    }

    // ProjectOutcomeUnknownAsync

    [Fact]
    public async Task ProjectOutcomeUnknownAsync_SetsInteractionTechnicalMetadata()
    {
        // Arrange
        var harness = CreateHarness();

        // Act
        await harness.Executor.ProjectOutcomeUnknownAsync(
            harness.Command,
            "provider_timeout",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("provider_timeout", harness.Interaction.TechnicalMetadata["providerErrorCode"]);
    }

    [Fact]
    public async Task ProjectOutcomeUnknownAsync_SetsActivityStatusToDialing()
    {
        // Arrange
        var harness = CreateHarness();

        // Act
        await harness.Executor.ProjectOutcomeUnknownAsync(
            harness.Command,
            "provider_timeout",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityStatus.Dialing, harness.Activity.Status);
    }

    // ProjectSuccessAsync — call session hydration

    [Fact]
    public async Task ProjectSuccessAsync_WhenSessionHasNoProviderCallId_HydratesProviderCallIdFromResult()
    {
        // Arrange
        var session = new CallSession { ItemId = "session-1", InteractionId = "interaction-1", State = ContactCenterCallState.Planned };
        var harness = CreateHarness(callSession: session);
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-99",
            ProviderName = "provider",
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("call-99", harness.Session.ProviderCallId);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenSessionHasNoProviderCallId_SetsSessionStateToRinging()
    {
        // Arrange
        var session = new CallSession { ItemId = "session-1", InteractionId = "interaction-1", State = ContactCenterCallState.Planned };
        var harness = CreateHarness(callSession: session);
        var result = new ContactCenterVoiceProviderResult { Succeeded = true, ProviderCallId = "call-1" };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ContactCenterCallState.Ringing, harness.Session.State);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenSessionHasNoProviderCallId_SetsProviderNameFromResult()
    {
        // Arrange
        var session = new CallSession { ItemId = "session-1", InteractionId = "interaction-1", State = ContactCenterCallState.Planned };
        var harness = CreateHarness(callSession: session);
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-1",
            ProviderName = "result-provider",
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("result-provider", harness.Session.ProviderName);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenSessionHasNoProviderCallId_SetsProviderNameFromCommandWhenResultProviderNameIsEmpty()
    {
        // Arrange
        var session = new CallSession { ItemId = "session-1", InteractionId = "interaction-1", State = ContactCenterCallState.Planned };
        var harness = CreateHarness(callSession: session);
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-1",
            ProviderName = string.Empty,
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("provider", harness.Session.ProviderName);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenSessionAlreadyHasProviderCallId_DoesNotOverwriteExistingId()
    {
        // Arrange
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            State = ContactCenterCallState.Ringing,
            ProviderCallId = "existing-call-id",
        };
        var harness = CreateHarness(callSession: session);
        var result = new ContactCenterVoiceProviderResult { Succeeded = true, ProviderCallId = "new-call-id" };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("existing-call-id", harness.Session.ProviderCallId);
        harness.CallSessionManager.Verify(
            m => m.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenSessionHasDifferentProviderCallId_DoesNotRewriteInteractionOrSession()
    {
        // Arrange — session already bound to "call-old", result carries "call-new"
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            State = ContactCenterCallState.Ringing,
            ProviderCallId = "call-old",
        };
        var harness = CreateHarness(callSession: session);
        var result = new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = "call-new",
            ProviderName = "provider",
        };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert — the interaction must NOT be repointed at the new call id
        Assert.True(
            string.IsNullOrEmpty(harness.Interaction.ProviderInteractionId) ||
            !string.Equals("call-new", harness.Interaction.ProviderInteractionId, StringComparison.Ordinal),
            "Interaction.ProviderInteractionId must not be repointed when the session already owns a different call id.");

        // Assert — the session's call id must remain unchanged
        Assert.Equal("call-old", harness.Session.ProviderCallId);

        // Assert — no mutation was persisted to the session
        harness.CallSessionManager.Verify(
            m => m.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenResultHasNoProviderCallId_DoesNotUpdateSession()
    {
        // Arrange
        var session = new CallSession { ItemId = "session-1", InteractionId = "interaction-1", State = ContactCenterCallState.Planned };
        var harness = CreateHarness(callSession: session);
        var result = new ContactCenterVoiceProviderResult { Succeeded = true, ProviderCallId = string.Empty };

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(string.IsNullOrEmpty(harness.Session.ProviderCallId));
        harness.CallSessionManager.Verify(
            m => m.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenCallSessionManagerIsNull_DoesNotThrow()
    {
        // Arrange
        var harness = CreateHarness();
        var result = new ContactCenterVoiceProviderResult { Succeeded = true, ProviderCallId = "call-1" };

        // Act / Assert — no exception when callSessionManager is null
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProjectSuccessAsync_WhenSessionHasNoProviderCallId_CallControlAuthorizationNowAuthorizes()
    {
        // Arrange
        var agentId = "agent-1";
        var userId = "user-1";
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "interaction-1",
            AgentId = agentId,
            State = ContactCenterCallState.Planned,
        };
        var harness = CreateHarness(callSession: session);
        var result = new ContactCenterVoiceProviderResult { Succeeded = true, ProviderCallId = "call-99", ProviderName = "provider" };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(m => m.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = agentId, UserId = userId });

        var sessionManagerForAuth = new Mock<ICallSessionManager>();
        sessionManagerForAuth
            .Setup(m => m.FindByInteractionIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var authService = new CallControlAuthorizationService(
            agentManager.Object,
            sessionManagerForAuth.Object,
            Mock.Of<ISupervisorQueueAuthorizationService>());

        // Act
        await harness.Executor.ProjectSuccessAsync(harness.Command, result, TestContext.Current.CancellationToken);

        var authResult = await authService.AuthorizeAsync(
            new CallControlAuthorizationContext
            {
                UserId = userId,
                InteractionId = "interaction-1",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(authResult.Succeeded);
    }

    private static TestHarness CreateHarness(
        bool canDispatch = true,
        IList<IProviderCommandDispatchValidator> validators = null,
        CallSession callSession = null)
    {
        var command = new ProviderCommand
        {
            CommandId = "command-1",
            CommandType = ProviderCommandType.Dial,
            ProviderName = "provider",
            ActivityItemId = "activity-1",
            InteractionId = "interaction-1",
            RequestPayload = """{"ActivityId":"activity-1","InteractionId":"interaction-1","Destination":"+15551112222"}""",
        };
        var claim = new ProviderCommandClaim
        {
            CommandId = command.CommandId,
            FenceToken = 1,
            OwnerToken = "owner-1",
        };
        var interaction = new Interaction { ItemId = command.InteractionId };
        var activity = new OmnichannelActivity { ItemId = command.ActivityItemId };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(value => value.FindByIdAsync(command.InteractionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(value => value.FindByIdAsync(command.ActivityItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var validator = new Mock<IProviderCommandDispatchValidator>();
        validator
            .Setup(v => v.CanDispatchAsync(It.IsAny<ProviderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canDispatch);
        var router = new Mock<IVoiceContactCenterCallRouter>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var actualValidators = validators ?? [validator.Object];

        Mock<ICallSessionManager> callSessionManager = null;

        if (callSession is not null)
        {
            callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager
                .Setup(m => m.FindByInteractionIdAsync(command.InteractionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(callSession);
            callSessionManager
                .Setup(m => m.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
        }

        var executor = new DialProviderCommandTypeExecutor(
            actualValidators,
            router.Object,
            interactionManager.Object,
            activityManager.Object,
            clock.Object,
            callSessionManager?.Object);

        return new TestHarness(command, claim, interaction, activity, validator, router, executor, callSessionManager, callSession);
    }

    private sealed record TestHarness(
        ProviderCommand Command,
        ProviderCommandClaim Claim,
        Interaction Interaction,
        OmnichannelActivity Activity,
        Mock<IProviderCommandDispatchValidator> Validator,
        Mock<IVoiceContactCenterCallRouter> Router,
        DialProviderCommandTypeExecutor Executor,
        Mock<ICallSessionManager> CallSessionManager,
        CallSession Session);
}
