using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIConversationWorkflowTests
{
    [Fact]
    public async Task StartingService_CollectsTheHandlersDecisions_AndSetsTheVariables()
    {
        var service = new DefaultAIConversationStartingService(
            [new DelegateHandler((_, result) => result.Variables["tier"] = "gold"), new DelegateHandler((_, result) => result.ProfileId = "vip")],
            NullLogger<DefaultAIConversationStartingService>.Instance);

        var conversation = new AIConversationContext { Direction = AIConversationDirections.Inbound };

        var result = await service.StartingAsync(conversation, TestContext.Current.CancellationToken);

        Assert.Equal("vip", result.ProfileId);
        Assert.Equal("gold", conversation.GetVariables()["tier"]);
        Assert.False(result.Declined);
    }

    [Fact]
    public async Task StartingService_StopsAtTheFirstDecline()
    {
        var calledAfterDecline = false;

        var service = new DefaultAIConversationStartingService(
            [new DelegateHandler((_, result) => result.Declined = true), new DelegateHandler((_, _) => calledAfterDecline = true)],
            NullLogger<DefaultAIConversationStartingService>.Instance);

        var result = await service.StartingAsync(new AIConversationContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Declined);
        Assert.False(calledAfterDecline);
    }

    [Fact]
    public async Task StartingService_SkipsAHandlerThatFails()
    {
        var service = new DefaultAIConversationStartingService(
            [new DelegateHandler((_, _) => throw new InvalidOperationException("boom")), new DelegateHandler((_, result) => result.Variables["x"] = "1")],
            NullLogger<DefaultAIConversationStartingService>.Instance);

        var result = await service.StartingAsync(new AIConversationContext(), TestContext.Current.CancellationToken);

        Assert.Equal("1", result.Variables["x"]);
    }

    [Fact]
    public void ParseLines_ReadsNameValueLines_AndClearsOnAnEmptyValue()
    {
        var values = SetConversationVariablesTask.ParseLines("first_name: Ada\n\nnot a variable\ntier:\r\nurl: https://example.com/a");

        Assert.Equal(
            [new("first_name", "Ada"), new("tier", null), new("url", "https://example.com/a")],
            values);
    }

    [Fact]
    public void ToolResult_IsTheWorkflowsOutput_WhenItFinished()
    {
        var context = CreateContext(WorkflowStatus.Finished);
        context.Output["slots"] = "9am, 2pm";

        using var json = JsonDocument.Parse(WorkflowToolFunction.ToToolResult(context));

        Assert.Equal("9am, 2pm", json.RootElement.GetProperty("slots").GetString());
    }

    [Fact]
    public void ToolResult_SaysTheRequestIsInHand_WhenTheWorkflowIsWaiting()
    {
        using var json = JsonDocument.Parse(WorkflowToolFunction.ToToolResult(CreateContext(WorkflowStatus.Halted)));

        Assert.Equal("accepted", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void ToolResult_IsAnError_WhenTheWorkflowFaulted()
    {
        var context = CreateContext(WorkflowStatus.Faulted);
        context.Workflow.FaultMessage = "The calendar is unreachable.";

        using var json = JsonDocument.Parse(WorkflowToolFunction.ToToolResult(context));

        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("The calendar is unreachable.", json.RootElement.GetProperty("message").GetString());
    }

    private static WorkflowExecutionContext CreateContext(WorkflowStatus status)
    {
        var workflow = new Workflow { Status = status, WorkflowId = "w1" };

        return new WorkflowExecutionContext(
            new WorkflowType { WorkflowTypeId = "t1" },
            workflow,
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);
    }

    private sealed class DelegateHandler : IAIConversationStartingHandler
    {
        private readonly Action<AIConversationContext, AIConversationStartingResult> _action;

        public DelegateHandler(Action<AIConversationContext, AIConversationStartingResult> action)
        {
            _action = action;
        }

        public Task StartingAsync(AIConversationContext conversation, AIConversationStartingResult result, CancellationToken cancellationToken)
        {
            _action(conversation, result);

            return Task.CompletedTask;
        }
    }
}
