using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Agent.Quality;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Agent.AIQuality;

/// <summary>
/// The test case, test run, alert rule and QA cohort tools refuse callers without the AI quality permission, apply the
/// checks of their editors and do what their admin pages do.
/// </summary>
public sealed class AIQualityToolsTests
{
    public static TheoryData<string> Tools => new()
    {
        ListAITestRunsTool.TheName,
        SaveAITestCaseTool.TheName,
        DeleteAITestCasesTool.TheName,
        SaveAIAlertRuleTool.TheName,
        DeleteAIAlertRuleTool.TheName,
        ListAIQACohortsTool.TheName,
        SaveAIQACohortTool.TheName,
        DeleteAIQACohortTool.TheName,
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAnyCheck(string toolName)
    {
        // Arrange
        var harness = CreateHarness();
        harness.SignedIn = false;

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(harness.Checks);
        Assert.Equal(0, harness.Writes);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenTheCallerLacksTheQualityPermission_Refuses(string toolName)
    {
        // Arrange
        var harness = CreateHarness();
        harness.Authorize = (_, _) => false;

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal("ManageAIQuality", harness.Checks[0]);
        Assert.Equal(0, harness.Writes);
    }

    [Fact]
    public async Task ListAITestRuns_ReportsTheBatchOutcomeOverTheWholeBatch()
    {
        // Arrange
        var harness = CreateHarness();

        for (var index = 0; index < 4; index++)
        {
            harness.Runs.Add(new AITestRun
            {
                ItemId = $"run-{index}",
                TestCaseId = "test-1",
                BatchId = "batch-1",
                QueuedUtc = AIQualityToolHarness.Now.AddMinutes(index),
                Status = index switch { 0 => AITestRunStatus.Passed, 1 => AITestRunStatus.Failed, _ => AITestRunStatus.Queued },
                Score = index == 1 ? 40 : 90,
                Criteria = index == 1 ? [new AIConversationCriterionResult { Name = "Refund offered", Met = false, Reason = "It never offered one." }] : [],
            });
        }

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAITestRunsTool(), services, new() { ["batchId"] = "batch-1", ["pageSize"] = 2 });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var failed = json.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetString() == "run-1");

        Assert.Equal(4, json.GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("pending").GetInt32());
        Assert.True(json.GetProperty("truncated").GetBoolean());
        Assert.False(failed.GetProperty("passed").GetBoolean());
        Assert.Equal("It never offered one.", Assert.Single(failed.GetProperty("criteria").EnumerateArray()).GetProperty("reason").GetString());
        Assert.Equal("2026-11-17T09:01:00+00:00", failed.GetProperty("queuedAt").GetString());
    }

    [Fact]
    public async Task ListAITestRuns_RefusesAnUnknownTestCase()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAITestRunsTool(), services, new() { ["testCaseId"] = "missing" });

        // Assert
        Assert.StartsWith("No test case has the ID 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAITestCase_StartsFromAConversation()
    {
        // Arrange
        var harness = CreateHarness();
        harness.AddConversation("session-1", "support", "My order never arrived.", "I want a refund now.");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAITestCaseTool(), services, new()
        {
            ["fromConversationId"] = "session-1",
            ["maxTurns"] = 5,
            ["mockedTools"] = new JsonObject { ["issueRefund"] = "{\"ok\":true}" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var testCase = harness.TestCases.Single(item => item.ItemId != "test-1");

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("support", testCase.ProfileId);
        Assert.Equal("session-1", testCase.SourceSessionId);
        Assert.Contains("I want a refund now.", testCase.Scenario, StringComparison.Ordinal);
        Assert.Equal(5, testCase.MaxTurns);
        Assert.Equal("{\"ok\":true}", testCase.MockedTools["issueRefund"]);
        Assert.Equal(AIQualityToolHarness.Now, testCase.CreatedUtc);
    }

    [Fact]
    public async Task SaveAITestCase_RefusesWhatTheEditorRefuses()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAITestCaseTool(), services, new()
        {
            ["testCaseId"] = "test-1",
            ["maxTurns"] = 80,
        });

        // Assert
        Assert.Equal("Nothing was saved: The conversation must have between 1 and 50 customer turns.", result);
        Assert.Equal(0, harness.Writes);
    }

    [Fact]
    public async Task DeleteAITestCases_DeletesAndSkipsTheMissing()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAITestCasesTool(), services, new() { ["testCaseIds"] = new[] { "test-1", "missing" } });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Single(json.GetProperty("deleted").EnumerateArray());
        Assert.Single(json.GetProperty("skipped").EnumerateArray());
        Assert.Empty(harness.TestCases);
    }

    [Fact]
    public async Task SaveAIAlertRule_CreatesARuleThatEmailsWhenTheFailureRateRises()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIAlertRuleTool(), services, new()
        {
            ["name"] = "Support QA failures",
            ["metric"] = "QAFailureRate",
            ["profileId"] = "support",
            ["condition"] = "Above",
            ["threshold"] = 20,
            ["windowMinutes"] = 60,
            ["notifyEmails"] = new[] { "admin@example.com" },
        });

        // Assert
        var rule = harness.Rules.Single(item => item.ItemId != "rule-1");

        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("created").GetBoolean());
        Assert.Equal(AIConversationMetric.QAFailureRate, rule.Metric);
        Assert.Equal(20, rule.Threshold);
        Assert.Equal("admin@example.com", rule.NotifyEmails);
        Assert.Equal(AIQualityToolHarness.Now, rule.CreatedUtc);
    }

    [Fact]
    public async Task SaveAIAlertRule_ChangingWhatIsWatchedChecksTheRuleAgainAtOnce()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Rules[0].LastCheckedUtc = AIQualityToolHarness.Now;

        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveAIAlertRuleTool(), services, new() { ["ruleId"] = "rule-1", ["notifyEmails"] = new[] { "ops@example.com" } });
        var untouched = harness.Rules[0].LastCheckedUtc;
        await AgentToolTestContext.InvokeAsync(new SaveAIAlertRuleTool(), services, new() { ["ruleId"] = "rule-1", ["threshold"] = 35 });

        // Assert
        Assert.Equal(AIQualityToolHarness.Now, untouched);
        Assert.Null(harness.Rules[0].LastCheckedUtc);
        Assert.Equal(35, harness.Rules[0].Threshold);
    }

    [Theory]
    [InlineData("windowMinutes", 50000, "The window must be between 1 minute and 30 days.")]
    [InlineData("checkEveryMinutes", 0, "Check between every minute and once a day.")]
    public async Task SaveAIAlertRule_RefusesWhatTheEditorRefuses(string argument, int value, string expected)
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIAlertRuleTool(), services, new() { ["ruleId"] = "rule-1", [argument] = value });

        // Assert
        Assert.Equal($"Nothing was saved: {expected}", result);
        Assert.Equal(0, harness.Writes);
    }

    [Fact]
    public async Task DeleteAIAlertRule_DeletesTheRuleWithItsIncidents()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Rules[0].Incidents.Add(new AIAlertIncident { OpenedUtc = AIQualityToolHarness.Now });

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIAlertRuleTool(), services, new() { ["ruleIds"] = new[] { "rule-1", "missing" } });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var deleted = Assert.Single(json.GetProperty("deleted").EnumerateArray());

        Assert.Equal(1, deleted.GetProperty("incidentsDeleted").GetInt32());
        Assert.Equal("missing", Assert.Single(json.GetProperty("skipped").EnumerateArray()).GetProperty("id").GetString());
        Assert.Empty(harness.Rules);
    }

    [Fact]
    public async Task ListAIQACohorts_ListsTheProfilesByTitle()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIQACohortsTool(), services);

        // Assert
        var cohort = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        Assert.Equal("Support", Assert.Single(cohort.GetProperty("profiles").EnumerateArray()).GetProperty("title").GetString());
        Assert.Equal("QA: Accuracy", cohort.GetProperty("label").GetString());
    }

    [Fact]
    public async Task SaveAIQACohort_CreatesACohortThatScoresFromNowOn()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIQACohortTool(), services, new()
        {
            ["name"] = "Sales accuracy",
            ["profileIds"] = new[] { "support" },
            ["samplePercent"] = 10,
            ["criteria"] = "The answer is accurate.",
        });

        // Assert
        var cohort = harness.Cohorts.Single(item => item.ItemId != "cohort-1");

        Assert.Contains("paid judge model call", AgentToolTestContext.ReadJson(result).GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal(10, cohort.SamplePercent);
        Assert.Equal(AIQualityToolHarness.Now, cohort.CursorUtc);
        Assert.True(cohort.Enabled);
    }

    [Fact]
    public async Task SaveAIQACohort_RefusesAnUnknownProfileAndABadSample()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var unknown = await AgentToolTestContext.InvokeAsync(new SaveAIQACohortTool(), services, new() { ["name"] = "X", ["profileIds"] = new[] { "ghost" } });
        var sample = await AgentToolTestContext.InvokeAsync(new SaveAIQACohortTool(), services, new() { ["cohortId"] = "cohort-1", ["samplePercent"] = 0 });

        // Assert
        Assert.Contains("no AI profile has the ID 'ghost'", unknown, StringComparison.Ordinal);
        Assert.Equal("Nothing was saved: The sample must be between 1 and 100 percent.", sample);
        Assert.Equal(0, harness.Writes);
    }

    [Fact]
    public async Task DeleteAIQACohort_DeletesTheCohort()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIQACohortTool(), services, new() { ["cohortIds"] = new[] { "cohort-1", "missing" } });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Single(json.GetProperty("deleted").EnumerateArray());
        Assert.Equal("missing", Assert.Single(json.GetProperty("skipped").EnumerateArray()).GetProperty("id").GetString());
        Assert.Empty(harness.Cohorts);
    }

    [Fact]
    public async Task ListAIQACohorts_APagePastTheEndIsEmpty()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIQACohortsTool(), services, new() { ["page"] = 3 });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Empty(json.GetProperty("items").EnumerateArray());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.False(json.GetProperty("truncated").GetBoolean());
    }

    private static AIQualityToolHarness CreateHarness()
    {
        var harness = new AIQualityToolHarness();

        harness.Profiles.Add(new AIProfile { ItemId = "support", Name = "support", DisplayText = "Support" });
        harness.TestCases.Add(new AITestCase { ItemId = "test-1", Name = "Refund", ProfileId = "support", Scenario = "Wants a refund." });
        harness.Rules.Add(new AIAlertRule { ItemId = "rule-1", Name = "Failures", Threshold = 20 });
        harness.Cohorts.Add(new AIQACohort { ItemId = "cohort-1", Name = "Accuracy", ProfileIds = ["support"] });

        return harness;
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
        => toolName switch
        {
            ListAITestRunsTool.TheName => (new ListAITestRunsTool(), new() { ["testCaseId"] = "test-1" }),
            SaveAITestCaseTool.TheName => (new SaveAITestCaseTool(), new() { ["testCaseId"] = "test-1", ["name"] = "Changed" }),
            DeleteAITestCasesTool.TheName => (new DeleteAITestCasesTool(), new() { ["testCaseIds"] = new[] { "test-1" } }),
            SaveAIAlertRuleTool.TheName => (new SaveAIAlertRuleTool(), new() { ["ruleId"] = "rule-1", ["threshold"] = 30 }),
            DeleteAIAlertRuleTool.TheName => (new DeleteAIAlertRuleTool(), new() { ["ruleIds"] = new[] { "rule-1" } }),
            ListAIQACohortsTool.TheName => (new ListAIQACohortsTool(), []),
            SaveAIQACohortTool.TheName => (new SaveAIQACohortTool(), new() { ["cohortId"] = "cohort-1", ["samplePercent"] = 50 }),
            DeleteAIQACohortTool.TheName => (new DeleteAIQACohortTool(), new() { ["cohortIds"] = new[] { "cohort-1" } }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
