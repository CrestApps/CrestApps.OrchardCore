using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Agent.Quality;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Agent.AIQuality;

/// <summary>
/// The test run and test-from-conversation tools refuse callers without the AI quality permission, apply the checks of
/// the test case editor and do what their admin pages do. Alert rules, QA cohorts and test cases themselves are record
/// kinds of the generic record tools (see <c>AIQualityRecordKindsTests</c>).
/// </summary>
public sealed class AIQualityToolsTests
{
    public static TheoryData<string> Tools => new()
    {
        ListAITestRunsTool.TheName,
        CreateAITestCaseFromConversationTool.TheName,
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
    public async Task CreateAITestCaseFromConversation_PlaysTheCustomerAgainAndKeepsTheConversation()
    {
        // Arrange
        var harness = CreateHarness();
        harness.AddConversation("session-1", "support", "My order never arrived.", "I want a refund now.");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CreateAITestCaseFromConversationTool(), services, new()
        {
            ["conversationId"] = "session-1",
            ["criteria"] = "A refund or a replacement is offered.",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var testCase = harness.TestCases.Single(item => item.ItemId != "test-1");

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(testCase.ItemId, json.GetProperty("id").GetString());
        Assert.Equal("support", testCase.ProfileId);
        Assert.Equal("session-1", testCase.SourceSessionId);
        Assert.Equal("Angry customer", testCase.Name);
        Assert.Equal("A refund or a replacement is offered.", testCase.Criteria);
        Assert.Contains("I want a refund now.", testCase.Scenario, StringComparison.Ordinal);
        Assert.Equal(AIQualityToolHarness.Now, testCase.CreatedUtc);
    }

    [Fact]
    public async Task CreateAITestCaseFromConversation_RefusesAnUnknownConversation()
    {
        // Arrange
        var harness = CreateHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CreateAITestCaseFromConversationTool(), services, new() { ["conversationId"] = "missing" });

        // Assert
        Assert.StartsWith("Nothing was saved: no conversation has the ID 'missing'", result, StringComparison.Ordinal);
        Assert.Equal(0, harness.Writes);
    }

    [Fact]
    public async Task CreateAITestCaseFromConversation_RefusesAConversationWhoseProfileIsGone()
    {
        // Arrange
        var harness = CreateHarness();
        harness.AddConversation("session-2", "deleted-profile", "Where is my parcel?");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CreateAITestCaseFromConversationTool(), services, new() { ["conversationId"] = "session-2" });

        // Assert
        Assert.Contains("'deleted-profile' no longer exists", result, StringComparison.Ordinal);
        Assert.Equal(0, harness.Writes);
    }

    private static AIQualityToolHarness CreateHarness()
    {
        var harness = new AIQualityToolHarness();

        harness.Profiles.Add(new AIProfile { ItemId = "support", Name = "support", DisplayText = "Support" });
        harness.TestCases.Add(new AITestCase { ItemId = "test-1", Name = "Refund", ProfileId = "support", Scenario = "Wants a refund." });

        return harness;
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
        => toolName switch
        {
            ListAITestRunsTool.TheName => (new ListAITestRunsTool(), new() { ["testCaseId"] = "test-1" }),
            CreateAITestCaseFromConversationTool.TheName => (new CreateAITestCaseFromConversationTool(), new() { ["conversationId"] = "session-1" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
