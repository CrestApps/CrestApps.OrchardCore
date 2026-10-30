using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Users;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What the AI Call Action task reports back, which is what the model reads when it runs the workflow as a tool.
/// </summary>
public sealed class AICallActionTaskTests
{
    [Fact]
    public async Task AFailedAction_IsReportedAsAFailure_WithTheReason()
    {
        // Arrange
        // Live, a refused profile switch reached the model as done: it told the caller they were being put through,
        // and then transferred them to a person nobody had asked for.
        var supervision = new Mock<IAIVoiceCallSupervision>();
        supervision
            .Setup(x => x.SwitchProfileAsync("activity-1", "profile-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIVoiceCallActionResult(false, "A live call keeps the AI profile it started with."));

        var task = CreateTask(supervision.Object);
        var context = CreateContext();

        // Act
        var result = await task.ExecuteAsync(context, null!);

        // Assert
        Assert.Contains("Failed", result.Outcomes);
        Assert.Equal("failed", context.Output["status"]);
        Assert.Equal("A live call keeps the AI profile it started with.", context.Output["message"]);
    }

    [Fact]
    public async Task ASuccessfulAction_ReportsNoFailure()
    {
        // Arrange
        var supervision = new Mock<IAIVoiceCallSupervision>();
        supervision
            .Setup(x => x.SwitchProfileAsync("activity-1", "profile-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIVoiceCallActionResult(true, "The call is handed to the new AI profile."));

        var task = CreateTask(supervision.Object);
        var context = CreateContext();

        // Act
        var result = await task.ExecuteAsync(context, null!);

        // Assert
        Assert.Contains("Done", result.Outcomes);
        Assert.False(context.Output.ContainsKey("status"));
    }

    private static AICallActionTask CreateTask(IAIVoiceCallSupervision supervision)
    {
        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        evaluator
            .Setup(service => service.EvaluateAsync(
                It.IsAny<WorkflowExpression<string>>(),
                It.IsAny<WorkflowExecutionContext>(),
                It.IsAny<System.Text.Encodings.Web.TextEncoder>()))
            .Returns((WorkflowExpression<string> expression, WorkflowExecutionContext _, System.Text.Encodings.Web.TextEncoder _) =>
                Task.FromResult(expression.Expression));

        return new AICallActionTask(
            [supervision],
            Mock.Of<IAgentProfileManager>(),
            new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null).Object,
            evaluator.Object,
            NullLogger<AICallActionTask>.Instance,
            new PassThroughStringLocalizer<AICallActionTask>())
        {
            ActivityId = "activity-1",
            Action = AICallActions.SwitchProfile,
            Value = "profile-2",
        };
    }

    private static WorkflowExecutionContext CreateContext()
        => new(
            new WorkflowType { WorkflowTypeId = "t1" },
            new Workflow { WorkflowId = "w1" },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);
}
