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

    private static TestHarness CreateHarness(
        bool canDispatch = true,
        IList<IProviderCommandDispatchValidator> validators = null)
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
        var executor = new DialProviderCommandTypeExecutor(
            actualValidators,
            router.Object,
            interactionManager.Object,
            activityManager.Object,
            clock.Object);

        return new TestHarness(command, claim, interaction, activity, validator, router, executor);
    }

    private sealed record TestHarness(
        ProviderCommand Command,
        ProviderCommandClaim Claim,
        Interaction Interaction,
        OmnichannelActivity Activity,
        Mock<IProviderCommandDispatchValidator> Validator,
        Mock<IVoiceContactCenterCallRouter> Router,
        DialProviderCommandTypeExecutor Executor);
}
