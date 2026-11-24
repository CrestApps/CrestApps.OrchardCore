using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using OrchardCore.ContentManagement.Workflows;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Tests.Agent.Workflows;

/// <summary>
/// The workflow tools: activity schemas come from the schema service; workflows are created, copied, enabled,
/// disabled and deleted through the workflow type store; instances are read from the instance store and the journal and
/// run, retried, restarted, cancelled, deleted and signalled through the workflow manager, as the Workflows admin pages
/// do.
/// </summary>
public sealed class WorkflowToolsTests
{
    [Fact]
    public async Task GetWorkflowActivity_ReturnsTheSchemaOutcomesAndOutputs()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetWorkflowActivityTool(), services, new() { ["names"] = new[] { "notifytask", "Missing" } });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var activity = Assert.Single(json.GetProperty("activities").EnumerateArray());

        Assert.Equal("NotifyTask", activity.GetProperty("name").GetString());
        Assert.Equal("task", activity.GetProperty("kind").GetString());
        Assert.Equal("Done", Assert.Single(activity.GetProperty("outcomes").EnumerateArray()).GetString());
        Assert.Equal("Message", Assert.Single(activity.GetProperty("propertiesSchema").GetProperty("required").EnumerateArray()).GetString());
        Assert.Equal("Sent", Assert.Single(activity.GetProperty("outputs").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("Missing", Assert.Single(json.GetProperty("unknown").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task GetWorkflowActivity_WithOnlyUnknownNames_SaysSo()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetWorkflowActivityTool(), services, new() { ["names"] = new[] { "Missing" } });

        // Assert
        Assert.StartsWith("There is no activity named 'Missing'", result);
    }

    [Fact]
    public async Task EditWorkflowDraft_CreatesADisabledWorkflowWithGeneratedIds()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new EditWorkflowDraftTool(), services, new()
        {
            ["name"] = "Blog post published",
            ["operations"] = JsonNode.Parse("""
                [
                  { "op": "addActivity", "alias": "start", "activity": "ContentPublishedEvent", "isStart": true },
                  { "op": "addActivity", "alias": "tell", "activity": "NotifyTask", "properties": { "Message": "A post was published." } },
                  { "op": "connect", "source": "start", "outcome": "Done", "destination": "tell" },
                  { "op": "setSettings", "settings": { "instances": "One" } }
                ]
                """),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var workflowTypeId = json.GetProperty("workflowTypeId").GetString();
        var saved = harness.Types[workflowTypeId];
        var transition = Assert.Single(saved.Transitions);
        var checks = json.GetProperty("checks");

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.False(saved.IsEnabled);
        Assert.True(saved.IsSingleton);
        Assert.True(saved.RecordActivityData);
        Assert.All(saved.Activities, activity => Assert.StartsWith("generated-activity-", activity.ActivityId));
        Assert.Equal(saved.Activities[0].ActivityId, transition.SourceActivityId);
        Assert.Equal(saved.Activities[1].ActivityId, transition.DestinationActivityId);
        Assert.Equal("Done", transition.SourceOutcomeName);
        Assert.NotNull(saved.VersionId);

        // The checks that live in the Workflows module are said not to have run, never reported as passed.
        Assert.Equal("notRun", checks.GetProperty("designerValidation").GetString());
        Assert.Equal("notChecked", checks.GetProperty("expressions").GetString());
    }

    [Fact]
    public async Task EditWorkflowDraft_WithAMisspelledSettingOrOutcome_SavesNothingAndNamesTheClosestOnes()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();
        var count = harness.Types.Count;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new EditWorkflowDraftTool(), services, new()
        {
            ["name"] = "Broken",
            ["operations"] = JsonNode.Parse("""
                [
                  { "op": "addActivity", "alias": "start", "activity": "ContentPublishedEvent", "isStart": true },
                  { "op": "addActivity", "alias": "tell", "activity": "NotifyTask", "properties": { "message": "wrong case" } },
                  { "op": "connect", "source": "start", "outcome": "Dne", "destination": "tell" }
                ]
                """),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var messages = json.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("message").GetString()).ToList();

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal(count, harness.Types.Count);
        Assert.Contains(messages, message => message.StartsWith("'message' is not a setting of 'NotifyTask'. Did you mean Message?", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("has no outcome 'Dne'. Did you mean Done?", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EditWorkflowDraft_WithAnUnknownSettingOrChoice_SavesNothingAndNamesTheValidOnes()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();
        var count = harness.Types.Count;

        // Act: settings the schema does not declare, or values outside their choices, are refused, never dropped or
        // replaced by the default.
        var result = await AgentToolTestContext.InvokeAsync(new EditWorkflowDraftTool(), services, new()
        {
            ["name"] = "Bad settings",
            ["operations"] = JsonNode.Parse("""
                [
                  { "op": "addActivity", "alias": "start", "activity": "ContentPublishedEvent", "isStart": true },
                  { "op": "setSettings", "settings": { "instances": "Single", "branchingMode": "Every", "lockTimout": 100, "lockExpiration": "soon", "deleteFinishedWorkflows": "yes" } }
                ]
                """),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var messages = json.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("message").GetString()).ToList();

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal(count, harness.Types.Count);
        Assert.Contains("The setting 'instances' must be one of: Any, One, OnePerCorrelation.", messages);
        Assert.Contains("The setting 'branchingMode' must be one of: FirstOnly, All.", messages);
        Assert.Contains(messages, message => message.StartsWith("The setting 'lockTimout' does not exist. The settings are: instances, lockTimeout,", StringComparison.Ordinal));
        Assert.Contains("The setting 'lockExpiration' must be a whole number of milliseconds.", messages);
        Assert.Contains("The setting 'deleteFinishedWorkflows' must be true or false.", messages);
    }

    [Fact]
    public async Task EditWorkflowDraft_WithAnUnknownActivity_NamesTheClosestActivity()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new EditWorkflowDraftTool(), services, new()
        {
            ["name"] = "Typo",
            ["operations"] = JsonNode.Parse("""[ { "op": "addActivity", "alias": "start", "activity": "ContentPublishEvent", "isStart": true } ]"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var message = Assert.Single(json.GetProperty("errors").EnumerateArray()).GetProperty("message").GetString();

        Assert.StartsWith("There is no activity named 'ContentPublishEvent' on this site. Did you mean ContentPublishedEvent", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditWorkflowDraft_WithAnOutcomeThatCannotBeChecked_WarnsInsteadOfPassingIt()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act: the signal event has no registered outcomes in the fixture.
        var result = await AgentToolTestContext.InvokeAsync(new EditWorkflowDraftTool(), services, new()
        {
            ["name"] = "Signal",
            ["operations"] = JsonNode.Parse("""
                [
                  { "op": "addActivity", "alias": "start", "activity": "SignalEvent", "isStart": true },
                  { "op": "addActivity", "alias": "tell", "activity": "NotifyTask", "properties": { "Message": "Signalled." } },
                  { "op": "connect", "source": "start", "outcome": "Done", "destination": "tell" }
                ]
                """),
            ["validateOnly"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var warnings = json.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetProperty("message").GetString()).ToList();

        Assert.True(json.GetProperty("valid").GetBoolean());
        Assert.Contains(warnings, warning => warning.Contains("the outcome 'Done' was not checked", StringComparison.Ordinal));
        Assert.Equal("partlyChecked", json.GetProperty("checks").GetProperty("outcomes").GetString());
    }

    [Fact]
    public async Task EditWorkflowDraft_WithValidateOnly_ChecksWithoutSaving()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();
        var count = harness.Types.Count;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new EditWorkflowDraftTool(), services, new()
        {
            ["name"] = "Check me",
            ["operations"] = JsonNode.Parse("""[ { "op": "addActivity", "alias": "start", "activity": "ContentPublishedEvent", "isStart": true } ]"""),
            ["validateOnly"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("valid").GetBoolean());
        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal(count, harness.Types.Count);
    }

    [Fact]
    public async Task EditWorkflowDraft_ForAnExistingWorkflow_ChangesNothingAndReturnsTheDesigner()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();
        var before = harness.Types[WorkflowToolHarness.OrderWorkflowTypeId].Activities.Count;
        var count = harness.Types.Count;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new EditWorkflowDraftTool(), services, new()
        {
            ["workflowTypeId"] = WorkflowToolHarness.OrderWorkflowTypeId,
            ["operations"] = JsonNode.Parse("""[ { "op": "addActivity", "alias": "tell", "activity": "NotifyTask", "properties": { "Message": "Hi" } } ]"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal(WorkflowToolHarness.OrderWorkflowTypeId, json.GetProperty("workflowTypeId").GetString());
        Assert.StartsWith("Nothing was changed", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal(before, harness.Types[WorkflowToolHarness.OrderWorkflowTypeId].Activities.Count);
        Assert.Equal(count, harness.Types.Count);
    }

    [Fact]
    public async Task EditWorkflowDraft_WithAnOperationThatIsNotAvailable_SavesNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();
        var count = harness.Types.Count;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new EditWorkflowDraftTool(), services, new()
        {
            ["name"] = "Remove",
            ["operations"] = JsonNode.Parse("""[ { "op": "removeActivity", "alias": "start" } ]"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains("'removeActivity', which is not available", Assert.Single(json.GetProperty("errors").EnumerateArray()).GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(count, harness.Types.Count);
    }

    [Fact]
    public async Task ListWorkflowTypeVersions_ListsTheVersionsAndMarksThePublishedOne()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListWorkflowTypeVersionsTool(), services, new() { ["workflowTypeId"] = "Order follow-up" });

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        Assert.Equal(1, item.GetProperty("version").GetInt32());
        Assert.True(item.GetProperty("isPublished").GetBoolean());
        Assert.Equal("admin", item.GetProperty("createdBy").GetString());
        Assert.Equal("2026-11-01T09:00:00+00:00", item.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task ListWorkflowTypeVersions_WithAnUnknownWorkflow_SaysSo()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListWorkflowTypeVersionsTool(), services, new() { ["workflowTypeId"] = "nope" });

        // Assert
        Assert.StartsWith("There is no workflow 'nope'", result);
    }

    [Fact]
    public async Task CloneWorkflowType_CreatesADisabledCopyWithoutTheHttpUrl()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CloneWorkflowTypeTool(), services, new()
        {
            ["workflowTypeId"] = WorkflowToolHarness.HttpWorkflowTypeId,
            ["name"] = "Inbound lead v2",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var copy = harness.Types[json.GetProperty("workflowTypeId").GetString()];
        var original = harness.Types[WorkflowToolHarness.HttpWorkflowTypeId];

        Assert.Equal("Inbound lead v2", copy.Name);
        Assert.False(copy.IsEnabled);
        Assert.False(copy.Activities[0].Properties.ContainsKey("Url"));
        Assert.True(original.Activities[0].Properties.ContainsKey("Url"));
        Assert.True(original.IsEnabled);
    }

    [Fact]
    public async Task CloneWorkflowType_WithAnUnknownWorkflow_CopiesNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();
        var count = harness.Types.Count;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CloneWorkflowTypeTool(), services, new() { ["workflowTypeId"] = "nope" });

        // Assert
        Assert.StartsWith("Nothing was copied", result);
        Assert.Equal(count, harness.Types.Count);
    }

    [Fact]
    public async Task SetWorkflowTypeEnabled_DisablesAndEnablesTheLiveTypeWithoutANewVersion()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();
        var versions = harness.Versions.Count;

        // Act
        var disabled = await AgentToolTestContext.InvokeAsync(new SetWorkflowTypeEnabledTool(), services, new() { ["workflowTypeId"] = WorkflowToolHarness.OrderWorkflowTypeId, ["enabled"] = false });
        var isDisabled = !harness.Types[WorkflowToolHarness.OrderWorkflowTypeId].IsEnabled;
        var enabled = await AgentToolTestContext.InvokeAsync(new SetWorkflowTypeEnabledTool(), services, new() { ["workflowTypeId"] = WorkflowToolHarness.OrderWorkflowTypeId, ["enabled"] = true });

        // Assert
        Assert.True(isDisabled);
        Assert.True(AgentToolTestContext.ReadJson(disabled).GetProperty("changed").GetBoolean());
        Assert.True(AgentToolTestContext.ReadJson(enabled).GetProperty("isEnabled").GetBoolean());
        Assert.True(harness.Types[WorkflowToolHarness.OrderWorkflowTypeId].IsEnabled);
        Assert.Equal(versions, harness.Versions.Count);
    }

    [Fact]
    public async Task SetWorkflowTypeEnabled_WithAnUnknownWorkflow_ChangesNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetWorkflowTypeEnabledTool(), services, new() { ["workflowTypeId"] = "nope", ["enabled"] = true });

        // Assert
        Assert.StartsWith("Nothing was changed", result);
    }

    [Fact]
    public async Task SetWorkflowTypeEnabled_WithoutEnabled_ChangesNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();
        var wasEnabled = harness.Types[WorkflowToolHarness.OrderWorkflowTypeId].IsEnabled;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetWorkflowTypeEnabledTool(), services, new() { ["workflowTypeId"] = WorkflowToolHarness.OrderWorkflowTypeId });

        // Assert
        Assert.StartsWith("Nothing was changed: give enabled", result, StringComparison.Ordinal);
        Assert.Equal(wasEnabled, harness.Types[WorkflowToolHarness.OrderWorkflowTypeId].IsEnabled);
    }

    [Fact]
    public async Task DeleteWorkflowType_DeletesTheWorkflowAndReportsItsInstances()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteWorkflowTypeTool(), services, new() { ["workflowTypeIds"] = new[] { WorkflowToolHarness.OrderWorkflowTypeId } });

        // Assert
        var deleted = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("deleted").EnumerateArray());

        Assert.Equal(2, deleted.GetProperty("deletedInstances").GetInt32());
        Assert.Equal([WorkflowToolHarness.OrderWorkflowTypeId], harness.DeletedTypes);
    }

    [Fact]
    public async Task DeleteWorkflowType_WhenOneWorkflowIsUnknown_DeletesNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteWorkflowTypeTool(), services, new() { ["workflowTypeIds"] = new[] { WorkflowToolHarness.OrderWorkflowTypeId, "nope" } });

        // Assert
        Assert.StartsWith("Nothing was deleted", result);
        Assert.Empty(harness.DeletedTypes);
    }

    [Fact]
    public async Task ListWorkflowInstances_ListsTheInstancesOfAWorkflowNewestFirst()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListWorkflowInstancesTool(), services, new() { ["workflowTypeId"] = WorkflowToolHarness.OrderWorkflowTypeId });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(4813, items[0].GetProperty("id").GetInt64());
        Assert.Equal("Halted", items[0].GetProperty("status").GetString());
        Assert.Equal("Faulted", items[1].GetProperty("status").GetString());
        Assert.Equal(1, items[1].GetProperty("version").GetInt32());
        Assert.Equal("Order follow-up", items[1].GetProperty("workflowName").GetString());
    }

    [Fact]
    public async Task ListWorkflowInstances_WithAnUnknownWorkflow_SaysSo()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListWorkflowInstancesTool(), services, new() { ["workflowTypeId"] = "nope" });

        // Assert
        Assert.StartsWith("There is no workflow 'nope'", result);
    }

    [Fact]
    public async Task GetWorkflowInstance_ReturnsTheFaultedActivityAndTheJournal()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        harness.Types[WorkflowToolHarness.OrderWorkflowTypeId].Variables.Add(new WorkflowVariableDefinition { Name = "orderId", TypeName = "number" });
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetWorkflowInstanceTool(), services, new() { ["instanceId"] = "4812" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("notify", json.GetProperty("faultedActivityId").GetString());
        Assert.Equal("1042", json.GetProperty("variables").GetProperty("orderId").GetString());
        Assert.Equal(2, json.GetProperty("journal").GetArrayLength());
        Assert.Equal("Faulted", json.GetProperty("journal")[1].GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("state", out _));
    }

    [Fact]
    public async Task GetWorkflowInstance_WithASequence_ReturnsTheRecordedData()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetWorkflowInstanceTool(), services, new() { ["instanceId"] = "wf-faulted", ["sequence"] = 2 });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("notify", json.GetProperty("activityId").GetString());
        Assert.Equal("\"refused\"", json.GetProperty("data").GetProperty("LastResult").GetString());
    }

    [Fact]
    public async Task GetWorkflowInstance_WithAnUnknownInstance_SaysSo()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetWorkflowInstanceTool(), services, new() { ["instanceId"] = "999" });

        // Assert
        Assert.StartsWith("There is no workflow instance '999'", result);
    }

    [Fact]
    public async Task RunWorkflowType_StartsTheWorkflowWithItsInputsAndReturnsTheOutputs()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunWorkflowTypeTool(), services, new()
        {
            ["workflowTypeId"] = "Recalculate totals",
            ["inputs"] = JsonNode.Parse("""{ "orderid": 1042 }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var started = Assert.Single(harness.Started);

        Assert.Equal("start", started.StartActivityId);
        Assert.Equal(1042, ((JsonNode)started.Input["orderId"]).GetValue<int>());
        Assert.Equal(5000, json.GetProperty("instanceId").GetInt64());
        Assert.Equal("99", json.GetProperty("outputs").GetProperty("total").GetString());
    }

    [Fact]
    public async Task RunWorkflowType_WithAnInputOfTheWrongType_RunsNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunWorkflowTypeTool(), services, new()
        {
            ["workflowTypeId"] = WorkflowToolHarness.ManualWorkflowTypeId,
            ["inputs"] = JsonNode.Parse("""{ "orderId": "not a number" }"""),
        });

        // Assert
        Assert.StartsWith("Nothing was run: these inputs do not have a value of their type", result);
        Assert.Empty(harness.Started);
    }

    [Fact]
    public async Task RunWorkflowType_ForAnHttpStart_ReturnsTheUrlInsteadOfRunning()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunWorkflowTypeTool(), services, new() { ["workflowTypeId"] = WorkflowToolHarness.HttpWorkflowTypeId });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("started").GetBoolean());
        Assert.Equal("/workflows/invoke?token=abc", json.GetProperty("url").GetString());
        Assert.Equal("POST", json.GetProperty("method").GetString());
        Assert.Empty(harness.Started);
    }

    [Fact]
    public async Task RunWorkflowType_WhenTheWorkflowIsDisabled_RunsNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        harness.Types[WorkflowToolHarness.ManualWorkflowTypeId].IsEnabled = false;
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunWorkflowTypeTool(), services, new() { ["workflowTypeId"] = WorkflowToolHarness.ManualWorkflowTypeId });

        // Assert
        Assert.Contains("is disabled", result, StringComparison.Ordinal);
        Assert.Empty(harness.Started);
    }

    [Fact]
    public async Task RerunWorkflowInstances_Retry_RunsTheFaultedActivityAgain()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RerunWorkflowInstancesTool(), services, new()
        {
            ["mode"] = "retry",
            ["instanceIds"] = JsonNode.Parse("[4812, 4813]"),
        });

        // Assert
        var results = AgentToolTestContext.ReadJson(result).GetProperty("results").EnumerateArray().ToList();

        Assert.Equal([(4812L, "notify")], harness.Retried);
        Assert.True(results[0].GetProperty("done").GetBoolean());
        Assert.False(results[1].GetProperty("done").GetBoolean());
    }

    [Fact]
    public async Task RerunWorkflowInstances_Restart_RefusesAWaitingInstanceAndRestartsAFinishedOne()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RerunWorkflowInstancesTool(), services, new()
        {
            ["mode"] = "restart",
            ["instanceIds"] = JsonNode.Parse("[4813, \"wf-finished\"]"),
        });

        // Assert
        var results = AgentToolTestContext.ReadJson(result).GetProperty("results").EnumerateArray().ToList();

        Assert.False(results[0].GetProperty("done").GetBoolean());
        Assert.True(results[1].GetProperty("done").GetBoolean());
        Assert.Equal([harness.Types[WorkflowToolHarness.ManualWorkflowTypeId].Id], harness.Restarted);
    }

    [Fact]
    public async Task CancelWorkflowInstances_AbortsAWaitingInstanceAndResumesItsParent()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CancelWorkflowInstancesTool(), services, new() { ["instanceIds"] = JsonNode.Parse("[4813, 4814]") });

        // Assert
        var waiting = harness.Instances.Single(workflow => workflow.Id == 4813);

        Assert.Equal(1, AgentToolTestContext.ReadJson(result).GetProperty("cancelled").GetInt32());
        Assert.Equal(WorkflowStatus.Aborted, waiting.Status);
        Assert.Empty(waiting.BlockingActivities);
        Assert.Equal([4813L], harness.SavedInstances);
        Assert.Equal([4813L], harness.ResumedParents);
        Assert.Equal(WorkflowStatus.Finished, harness.Instances.Single(workflow => workflow.Id == 4814).Status);
    }

    [Fact]
    public async Task DeleteWorkflowInstances_DeletesTheInstances()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteWorkflowInstancesTool(), services, new() { ["instanceIds"] = JsonNode.Parse("[4813, 4814]") });

        // Assert
        Assert.Equal(2, AgentToolTestContext.ReadJson(result).GetProperty("deleted").GetArrayLength());
        Assert.Equal([4813L, 4814L], harness.DeletedInstances);
        Assert.Equal([4813L], harness.ResumedParents);
    }

    [Fact]
    public async Task DeleteWorkflowInstances_WhenOneInstanceIsUnknown_DeletesNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteWorkflowInstancesTool(), services, new() { ["instanceIds"] = JsonNode.Parse("[4813, 999]") });

        // Assert
        Assert.StartsWith("Nothing was deleted", result);
        Assert.Empty(harness.DeletedInstances);
    }

    [Fact]
    public async Task TriggerWorkflowSignal_WithACorrelationId_TriggersTheSignalEvent()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new TriggerWorkflowSignalTool(), services, new() { ["signal"] = "Approved", ["correlationId"] = "77" });

        // Assert
        var triggered = Assert.Single(harness.TriggeredEvents);

        Assert.Equal("SignalEvent", triggered.Name);
        Assert.Equal("Approved", triggered.Input["Signal"]);
        Assert.Equal("77", triggered.CorrelationId);
        Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("runs").EnumerateArray());
    }

    [Fact]
    public async Task TriggerWorkflowSignal_WithAnInstance_ResumesItsSignalEvent()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new TriggerWorkflowSignalTool(), services, new() { ["signal"] = "Approved", ["instanceId"] = "4813" });

        // Assert
        var resumed = Assert.Single(harness.Resumed);

        Assert.Equal(4813, resumed.InstanceId);
        Assert.Equal("signal", resumed.ActivityId);
        Assert.Equal("Approved", resumed.Input["Signal"]);
    }

    [Fact]
    public async Task TriggerWorkflowSignal_WithAnInstanceThatDoesNotWaitOnASignal_SendsNothing()
    {
        // Arrange
        var harness = new WorkflowToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new TriggerWorkflowSignalTool(), services, new() { ["signal"] = "Approved", ["instanceId"] = "4812" });

        // Assert
        Assert.StartsWith("Nothing was sent", result);
        Assert.Empty(harness.Resumed);
    }

    [Fact]
    public async Task CompleteWorkflowUserTask_WithoutAnAction_ListsTheActionsOfTheCallersRoles()
    {
        // Arrange
        var harness = new WorkflowToolHarness
        {
            Roles = ["Editor"],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompleteWorkflowUserTaskTool(), services, new() { ["contentItemId"] = "article-1" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("completed").GetBoolean());
        Assert.Equal(["Approve", "Reject"], json.GetProperty("availableActions").EnumerateArray().Select(action => action.GetString()));
        Assert.Empty(harness.TriggeredEvents);
    }

    [Fact]
    public async Task CompleteWorkflowUserTask_WithAnAction_TriggersTheUserTaskForTheItem()
    {
        // Arrange
        var harness = new WorkflowToolHarness
        {
            Roles = ["Editor"],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompleteWorkflowUserTaskTool(), services, new() { ["contentItemId"] = "article-1", ["action"] = "approve" });

        // Assert
        var triggered = Assert.Single(harness.TriggeredEvents);
        var contentEvent = Assert.IsType<ContentEventContext>(triggered.Input[ContentEventConstants.ContentEventInputKey]);

        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("completed").GetBoolean());
        Assert.Equal("UserTaskEvent", triggered.Name);
        Assert.Equal("Approve", triggered.Input[ContentEventConstants.UserActionInputKey]);
        Assert.Same(harness.ContentItem, triggered.Input[ContentEventConstants.ContentItemInputKey]);
        Assert.Equal("article-1-v2", contentEvent.ContentItemVersionId);
        Assert.Equal("article-1", triggered.CorrelationId);
    }

    [Fact]
    public async Task CompleteWorkflowUserTask_WhenTheCallerHasNoneOfTheRoles_OffersNoAction()
    {
        // Arrange
        var harness = new WorkflowToolHarness
        {
            Roles = ["Author"],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompleteWorkflowUserTaskTool(), services, new() { ["contentItemId"] = "article-1", ["action"] = "Approve" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("completed").GetBoolean());
        Assert.Empty(json.GetProperty("availableActions").EnumerateArray());
        Assert.Empty(harness.TriggeredEvents);
    }
}
