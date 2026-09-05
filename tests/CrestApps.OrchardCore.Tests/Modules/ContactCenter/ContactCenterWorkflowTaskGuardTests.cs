using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterWorkflowTaskGuardTests
{
    [Theory]
    [InlineData(AgentPresenceStatus.Reserved)]
    [InlineData(AgentPresenceStatus.Busy)]
    [InlineData(AgentPresenceStatus.WrapUp)]
    public async Task SetAgentPresenceTask_WhenStatusIsLifecycleOwned_ReturnsFailedWithoutCallingManager(AgentPresenceStatus status)
    {
        // Arrange
        var presenceManager = new Mock<IAgentPresenceManager>();
        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        var task = new SetAgentPresenceTask(
            presenceManager.Object,
            evaluator.Object,
            NullLogger<SetAgentPresenceTask>.Instance,
            new PassThroughStringLocalizer<SetAgentPresenceTask>())
        {
            UserId = "user-1",
            Status = status,
        };

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
        presenceManager.Verify(
            manager => manager.SetPresenceAsync(It.IsAny<string>(), It.IsAny<AgentPresenceStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetAgentPresenceTask_WhenStatusIsAllowed_CallsManagerAndReturnsDone()
    {
        // Arrange
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager
            .Setup(manager => manager.SetPresenceAsync("user-1", AgentPresenceStatus.Break, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile());
        var evaluator = CreateEchoEvaluator();
        var task = new SetAgentPresenceTask(
            presenceManager.Object,
            evaluator.Object,
            NullLogger<SetAgentPresenceTask>.Instance,
            new PassThroughStringLocalizer<SetAgentPresenceTask>())
        {
            UserId = "user-1",
            Status = AgentPresenceStatus.Break,
        };

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        presenceManager.Verify(
            manager => manager.SetPresenceAsync("user-1", AgentPresenceStatus.Break, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueActivityTask_WhenQueueDoesNotExist_ReturnsFailedWithoutEnqueue()
    {
        // Arrange
        var queueService = new Mock<IActivityQueueService>();
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.FindByIdAsync("q-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityQueue)null);
        var activityManager = new Mock<IOmnichannelActivityManager>();
        var task = CreateEnqueueTask(queueService, queueManager, activityManager);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
        queueService.Verify(
            service => service.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueActivityTask_WhenActivityDoesNotExist_ReturnsFailedWithoutEnqueue()
    {
        // Arrange
        var queueService = new Mock<IActivityQueueService>();
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.FindByIdAsync("q-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityQueue());
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OmnichannelActivity)null);
        var task = CreateEnqueueTask(queueService, queueManager, activityManager);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
        queueService.Verify(
            service => service.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueActivityTask_WhenQueueAndActivityExist_EnqueuesAndReturnsDone()
    {
        // Arrange
        var queueService = new Mock<IActivityQueueService>();
        queueService
            .Setup(service => service.EnqueueAsync("act-1", "q-1", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem());
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.FindByIdAsync("q-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityQueue());
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity());
        var task = CreateEnqueueTask(queueService, queueManager, activityManager);

        // Act
        var result = await task.ExecuteAsync(null!, null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        queueService.Verify(
            service => service.EnqueueAsync("act-1", "q-1", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static EnqueueActivityTask CreateEnqueueTask(
        Mock<IActivityQueueService> queueService,
        Mock<IActivityQueueManager> queueManager,
        Mock<IOmnichannelActivityManager> activityManager)
    {
        var evaluator = CreateEchoEvaluator();

        return new EnqueueActivityTask(
            queueService.Object,
            queueManager.Object,
            activityManager.Object,
            evaluator.Object,
            NullLogger<EnqueueActivityTask>.Instance,
            new PassThroughStringLocalizer<EnqueueActivityTask>())
        {
            ActivityItemId = "act-1",
            QueueId = "q-1",
        };
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
