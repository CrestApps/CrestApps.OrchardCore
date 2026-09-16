using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The two workflow activities that let a no-code automation reach a customer and hand them to a person: placing
/// the call (or sending the message), and transferring the conversation into the human lane.
/// </summary>
public sealed class OmnichannelWorkflowTaskTests
{
    // --- Place Call or Send Message --------------------------------------------------------------------------------

    [Fact]
    public async Task PlacingACall_StartsTheActivityOnItsOwnChannelsProcessor()
    {
        // Arrange
        // The channel comes from the activity, not from the task, so one activity places a call and sends a text.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone", Status = ActivityStatus.NotStated };
        var phone = CreateProcessor("Phone");
        var sms = CreateProcessor("SMS");
        var task = CreateStartTask(activity, [sms.Object, phone.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        phone.Verify(x => x.StartAsync(activity, It.IsAny<CancellationToken>()), Times.Once);
        sms.Verify(x => x.StartAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PlacingACall_MatchesTheChannelWithoutRegardToCasing()
    {
        // Arrange
        // Channels are stored as free text, and a recipe-authored activity may not match the processor's casing.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "phone", Status = ActivityStatus.Scheduled };
        var phone = CreateProcessor("Phone");
        var task = CreateStartTask(activity, [phone.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        phone.Verify(x => x.StartAsync(activity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ActivityStatus.Dialing)]
    [InlineData(ActivityStatus.InProgress)]
    [InlineData(ActivityStatus.AwaitingCustomerAnswer)]
    [InlineData(ActivityStatus.Completed)]
    public async Task ACustomerWhoIsAlreadyBeingCalled_IsNotCalledAgain(ActivityStatus status)
    {
        // Arrange
        // This is the important one. A workflow that fires twice — or races the periodic automated-activities
        // pass — must not place a second call to someone who is already on the line with us.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone", Status = status };
        var phone = CreateProcessor("Phone");
        var task = CreateStartTask(activity, [phone.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Already Started", result.Outcomes);
        phone.Verify(x => x.StartAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenTheChannelsModuleIsNotEnabled_TheCallIsNotPlaced()
    {
        // Arrange
        // A Phone activity on a tenant with no voice module. Reporting it beats throwing inside a workflow run.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone", Status = ActivityStatus.NotStated };
        var task = CreateStartTask(activity, [CreateProcessor("SMS").Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
    }

    [Fact]
    public async Task WhenTheActivityCannotBeFound_TheCallIsNotPlaced()
    {
        // Arrange
        var task = CreateStartTask(activity: null, [CreateProcessor("Phone").Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
    }

    [Fact]
    public async Task WhenAProcessorThrows_TheWorkflowIsToldRatherThanBroken()
    {
        // Arrange
        // A provider outage must surface as a branch the workflow can handle, not as an exception mid-run.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone", Status = ActivityStatus.NotStated };
        var phone = CreateProcessor("Phone");
        phone.Setup(x => x.StartAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The provider rejected the call."));
        var task = CreateStartTask(activity, [phone.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
    }

    // --- Transfer to Agent -----------------------------------------------------------------------------------------

    [Fact]
    public async Task TransferringACall_UsesTheHandoffThatOwnsThatChannel()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone", AISessionId = "session-1" };
        var smsHandoff = CreateHandoff("SMS", OmnichannelHandoffResult.Success());
        var voiceHandoff = CreateHandoff("Phone", OmnichannelHandoffResult.Success());
        var task = CreateTransferTask(activity, [smsHandoff.Object, voiceHandoff.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Connected", result.Outcomes);
        voiceHandoff.Verify(x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        smsHandoff.Verify(x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransferringAText_UsesTheHandoffThatOwnsThatChannel()
    {
        // Arrange
        // The same activity transfers a text conversation, which is the whole point of one channel-neutral task.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "SMS" };
        var smsHandoff = CreateHandoff("SMS", OmnichannelHandoffResult.Success(conversationId: "conversation-1"));
        var task = CreateTransferTask(activity, [smsHandoff.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Connected", result.Outcomes);
        smsHandoff.Verify(x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACallerLeftWaiting_IsReportedSeparatelyFromOneWhoWasConnected()
    {
        // Arrange
        // A workflow usually wants to say something different to someone who is holding than to someone who has
        // just been put through, so these cannot collapse into one outcome.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone" };
        var handoff = CreateHandoff("Phone", OmnichannelHandoffResult.WaitingInQueue());
        var task = CreateTransferTask(activity, [handoff.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Waiting In Queue", result.Outcomes);
    }

    [Fact]
    public async Task AnAfterHoursTransferThatBecameACallback_IsReportedAsOne()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone" };
        var handoff = CreateHandoff("Phone", OmnichannelHandoffResult.CallbackScheduled());
        var task = CreateTransferTask(activity, [handoff.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Callback Scheduled", result.Outcomes);
    }

    [Fact]
    public async Task ATransferCarriesTheTranscriptSessionSoTheAgentDoesNotStartOver()
    {
        // Arrange
        // The agent inheriting the conversation is the difference between a handoff and a cold restart.
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            Channel = "Phone",
            AISessionId = "session-1",
            PreferredDestination = "+15550000000",
        };
        var handoff = CreateHandoff("Phone", OmnichannelHandoffResult.Success());
        OmnichannelHandoffRequest captured = null;
        handoff.Setup(x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()))
            .Callback<OmnichannelHandoffRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(OmnichannelHandoffResult.Success());

        var task = CreateTransferTask(activity, [handoff.Object]);
        task.QueueId = "queue-7";
        task.Reason = "The customer asked for a person.";
        task.Summary = "Wants a small SUV under thirty grand.";

        // Act
        await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal("session-1", captured.AiSessionId);
        Assert.Equal("queue-7", captured.TargetQueueId);
        Assert.Equal("The customer asked for a person.", captured.Reason);
        Assert.Equal("Wants a small SUV under thirty grand.", captured.Summary);
        Assert.Equal("+15550000000", captured.ContactAddress);
    }

    [Fact]
    public async Task ATransferWithNoQueueNamed_LetsTheSubjectFlowChoose()
    {
        // Arrange
        // An empty queue is not a mistake: it means "use the queue an operator already configured for handoffs",
        // so it must reach the handoff as empty rather than as a queue that does not exist.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone" };
        var handoff = CreateHandoff("Phone", OmnichannelHandoffResult.Success());
        OmnichannelHandoffRequest captured = null;
        handoff.Setup(x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()))
            .Callback<OmnichannelHandoffRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(OmnichannelHandoffResult.Success());

        var task = CreateTransferTask(activity, [handoff.Object]);
        task.QueueId = null;

        // Act
        await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.True(string.IsNullOrEmpty(captured.TargetQueueId));
    }

    [Fact]
    public async Task WhenNoHumanDestinationIsConfigured_TheWorkflowIsToldRatherThanBroken()
    {
        // Arrange
        // For example the SMS workspace is not enabled on this tenant.
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "SMS" };
        var task = CreateTransferTask(activity, [CreateHandoff("Phone", OmnichannelHandoffResult.Success()).Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
    }

    [Fact]
    public async Task WhenTheHandoffThrows_TheWorkflowIsToldRatherThanBroken()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "activity-1", Channel = "Phone" };
        var handoff = CreateHandoff("Phone", OmnichannelHandoffResult.Success());
        handoff.Setup(x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The queue is gone."));
        var task = CreateTransferTask(activity, [handoff.Object]);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
    }

    // --- Helpers ---------------------------------------------------------------------------------------------------

    private static StartOmnichannelActivityTask CreateStartTask(OmnichannelActivity activity, IEnumerable<IOmnichannelProcessor> processors)
    {
        var manager = new Mock<IOmnichannelActivityManager>();
        manager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(activity);

        return new StartOmnichannelActivityTask(
            manager.Object,
            processors,
            CreateEchoEvaluator().Object,
            NullLogger<StartOmnichannelActivityTask>.Instance,
            new PassThroughStringLocalizer<StartOmnichannelActivityTask>())
        {
            ActivityItemId = "activity-1",
        };
    }

    private static TransferToAgentTask CreateTransferTask(OmnichannelActivity activity, IEnumerable<IOmnichannelHandoffService> handoffServices)
    {
        var manager = new Mock<IOmnichannelActivityManager>();
        manager.Setup(x => x.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(activity);

        return new TransferToAgentTask(
            manager.Object,
            handoffServices,
            CreateEchoEvaluator().Object,
            NullLogger<TransferToAgentTask>.Instance,
            new PassThroughStringLocalizer<TransferToAgentTask>())
        {
            ActivityItemId = "activity-1",
        };
    }

    private static Mock<IOmnichannelProcessor> CreateProcessor(string channel)
    {
        var processor = new Mock<IOmnichannelProcessor>();
        processor.SetupGet(x => x.Channel).Returns(channel);

        return processor;
    }

    private static Mock<IOmnichannelHandoffService> CreateHandoff(string channel, OmnichannelHandoffResult result)
    {
        var handoff = new Mock<IOmnichannelHandoffService>();
        handoff.Setup(x => x.CanHandle(It.IsAny<string>()))
            .Returns<string>(value => string.Equals(value, channel, StringComparison.OrdinalIgnoreCase));
        handoff.Setup(x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return handoff;
    }

    private static Mock<IWorkflowExpressionEvaluator> CreateEchoEvaluator()
    {
        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        evaluator
            .Setup(service => service.EvaluateAsync(
                It.IsAny<WorkflowExpression<string>>(),
                It.IsAny<WorkflowExecutionContext>(),
                It.IsAny<System.Text.Encodings.Web.TextEncoder>()))
            .Returns((WorkflowExpression<string> expression, WorkflowExecutionContext _, System.Text.Encodings.Web.TextEncoder _) =>
                Task.FromResult(expression.Expression));

        return evaluator;
    }
}
