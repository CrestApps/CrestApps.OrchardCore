using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Agent.Workflows;

/// <summary>
/// Every workflow tool acts for the person in the chat: it refuses a caller who is not signed in without checking
/// anything, and a caller who lacks the permission the matching admin page asks for, and it changes and runs nothing
/// when it refuses.
/// </summary>
public sealed class WorkflowToolCallerTests
{
    public static TheoryData<string, string> Tools => new()
    {
        { GetWorkflowActivityTool.TheName, "ManageWorkflows" },
        { EditWorkflowDraftTool.TheName, "ManageWorkflows" },
        { ListWorkflowTypeVersionsTool.TheName, "ManageWorkflows" },
        { CloneWorkflowTypeTool.TheName, "ManageWorkflows" },
        { SetWorkflowTypeEnabledTool.TheName, "ManageWorkflows" },
        { DeleteWorkflowTypeTool.TheName, "ManageWorkflows" },
        { ListWorkflowInstancesTool.TheName, "ManageWorkflows" },
        { GetWorkflowInstanceTool.TheName, "ManageWorkflows" },
        { RunWorkflowTypeTool.TheName, "ExecuteWorkflows" },
        { RerunWorkflowInstancesTool.TheName, "ManageWorkflows" },
        { CancelWorkflowInstancesTool.TheName, "ManageWorkflows" },
        { DeleteWorkflowInstancesTool.TheName, "ManageWorkflows" },
        { TriggerWorkflowSignalTool.TheName, "ExecuteWorkflows" },
        { CompleteWorkflowUserTaskTool.TheName, "EditContent" },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAnyCheck(string toolName, string permission)
    {
        // Arrange
        _ = permission;

        var harness = new WorkflowToolHarness
        {
            SignedIn = false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);
        var types = harness.Types.Count;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
        AssertNothingChanged(harness, types);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminPage_Refuses(string toolName, string permission)
    {
        // Arrange
        var harness = new WorkflowToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);
        var types = harness.Types.Count;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, checks[0].Permission);
        AssertNothingChanged(harness, types);
    }

    [Fact]
    public async Task RerunWorkflowInstances_Retry_WhenTheCallerMayManageButNotExecute_Refuses()
    {
        // Arrange
        var harness = new WorkflowToolHarness
        {
            Authorize = (permission, _) => permission.Name == "ManageWorkflows",
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(RerunWorkflowInstancesTool.TheName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(["ManageWorkflows", "ExecuteWorkflows"], checks.Select(check => check.Permission));
        Assert.Empty(harness.Retried);
    }

    [Fact]
    public async Task CompleteWorkflowUserTask_ChecksEditContentOnTheContentItem()
    {
        // Arrange
        var harness = new WorkflowToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(CompleteWorkflowUserTaskTool.TheName);

        // Act
        await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Same(harness.ContentItem, Assert.IsType<ContentItem>(check.Resource));
    }

    private static void AssertNothingChanged(WorkflowToolHarness harness, int types)
    {
        Assert.Equal(types, harness.Types.Count);
        Assert.True(harness.Types[WorkflowToolHarness.OrderWorkflowTypeId].IsEnabled);
        Assert.Empty(harness.DeletedTypes);
        Assert.Empty(harness.DeletedInstances);
        Assert.Empty(harness.SavedInstances);
        Assert.Empty(harness.Started);
        Assert.Empty(harness.Retried);
        Assert.Empty(harness.Restarted);
        Assert.Empty(harness.Resumed);
        Assert.Empty(harness.TriggeredEvents);
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
        => toolName switch
        {
            GetWorkflowActivityTool.TheName => (new GetWorkflowActivityTool(), new() { ["names"] = new[] { "NotifyTask" } }),
            EditWorkflowDraftTool.TheName => (new EditWorkflowDraftTool(), new()
            {
                ["name"] = "New workflow",
                ["operations"] = JsonNode.Parse("""[ { "op": "addActivity", "alias": "start", "activity": "ContentPublishedEvent", "isStart": true } ]"""),
            }),
            ListWorkflowTypeVersionsTool.TheName => (new ListWorkflowTypeVersionsTool(), new() { ["workflowTypeId"] = WorkflowToolHarness.OrderWorkflowTypeId }),
            CloneWorkflowTypeTool.TheName => (new CloneWorkflowTypeTool(), new() { ["workflowTypeId"] = WorkflowToolHarness.OrderWorkflowTypeId }),
            SetWorkflowTypeEnabledTool.TheName => (new SetWorkflowTypeEnabledTool(), new() { ["workflowTypeId"] = WorkflowToolHarness.OrderWorkflowTypeId, ["enabled"] = false }),
            DeleteWorkflowTypeTool.TheName => (new DeleteWorkflowTypeTool(), new() { ["workflowTypeIds"] = new[] { WorkflowToolHarness.OrderWorkflowTypeId } }),
            ListWorkflowInstancesTool.TheName => (new ListWorkflowInstancesTool(), []),
            GetWorkflowInstanceTool.TheName => (new GetWorkflowInstanceTool(), new() { ["instanceId"] = "4812" }),
            RunWorkflowTypeTool.TheName => (new RunWorkflowTypeTool(), new() { ["workflowTypeId"] = WorkflowToolHarness.ManualWorkflowTypeId }),
            RerunWorkflowInstancesTool.TheName => (new RerunWorkflowInstancesTool(), new() { ["mode"] = "retry", ["instanceIds"] = JsonNode.Parse("[4812]") }),
            CancelWorkflowInstancesTool.TheName => (new CancelWorkflowInstancesTool(), new() { ["instanceIds"] = JsonNode.Parse("[4813]") }),
            DeleteWorkflowInstancesTool.TheName => (new DeleteWorkflowInstancesTool(), new() { ["instanceIds"] = JsonNode.Parse("[4813]") }),
            TriggerWorkflowSignalTool.TheName => (new TriggerWorkflowSignalTool(), new() { ["signal"] = "Approved", ["instanceId"] = "4813" }),
            CompleteWorkflowUserTaskTool.TheName => (new CompleteWorkflowUserTaskTool(), new() { ["contentItemId"] = "article-1", ["action"] = "Approve" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
