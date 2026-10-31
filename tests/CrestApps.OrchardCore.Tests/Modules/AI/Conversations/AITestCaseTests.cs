using CrestApps.Core;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Drivers;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AITestCaseTests
{
    [Fact]
    public void FromConversation_PlaysTheCustomerAgainWithTheConversationsVariables()
    {
        // Arrange
        var session = new AIChatSession { SessionId = "s-1", ProfileId = "p-1", Title = "Refund for a double charge" };
        session.SetConversationVariable("order_id", "A-100");
        var prompts = new List<AIChatSessionPrompt>
        {
            new() { Role = ChatRole.Assistant, Content = "Hello, how can I help?" },
            new() { Role = ChatRole.User, Content = "I was charged twice." },
            new() { Role = ChatRole.User, Content = "Generated", IsGeneratedPrompt = true },
            new() { Role = ChatRole.User, Content = "I want my money back." },
        };

        // Act
        var testCase = AITestCaseFactory.FromConversation(session, prompts);

        // Assert
        Assert.Equal("Refund for a double charge", testCase.Name);
        Assert.Equal("p-1", testCase.ProfileId);
        Assert.Equal("s-1", testCase.SourceSessionId);
        Assert.Contains("I was charged twice.", testCase.Scenario, StringComparison.Ordinal);
        Assert.Contains("I want my money back.", testCase.Scenario, StringComparison.Ordinal);
        Assert.DoesNotContain("Generated", testCase.Scenario, StringComparison.Ordinal);
        Assert.DoesNotContain("how can I help", testCase.Scenario, StringComparison.Ordinal);
        Assert.Equal("A-100", testCase.Variables["order_id"]);
    }

    [Fact]
    public void FromConversation_OnAVersionCopy_TestsThatVersionOfTheSourceProfile()
    {
        // Arrange
        var session = new AIChatSession { SessionId = "s-2", ProfileId = "copy-1" };
        session.Put(new AIProfileVersionCopy { SourceProfileId = "p-1", Version = 3 });

        // Act
        var testCase = AITestCaseFactory.FromConversation(session, []);

        // Assert
        Assert.Equal("p-1", testCase.ProfileId);
        Assert.Equal(3, testCase.ProfileVersion);
    }

    [Fact]
    public void PassRate_CountsOnlyFinishedRuns()
    {
        // Arrange
        var runs = new[]
        {
            new AITestRun { Status = AITestRunStatus.Passed },
            new AITestRun { Status = AITestRunStatus.Passed },
            new AITestRun { Status = AITestRunStatus.Failed },
            new AITestRun { Status = AITestRunStatus.Error },
            new AITestRun { Status = AITestRunStatus.Queued },
        };

        // Act
        var rate = AITestRunner.PassRate(runs);

        // Assert
        Assert.Equal(2d / 3, rate);
        Assert.Null(AITestRunner.PassRate([new AITestRun { Status = AITestRunStatus.Queued }]));
    }

    [Fact]
    public void Pairs_RoundTripNameValueLines()
    {
        // Act
        var pairs = AITestCaseDisplayDriver.ParsePairs("balance_due: 120.50\nno colon here\n url : https://example.com/a:b \n");

        // Assert
        Assert.Equal(2, pairs.Count);
        Assert.Equal("120.50", pairs["balance_due"]);
        Assert.Equal("https://example.com/a:b", pairs["url"]);
        Assert.Equal(pairs, AITestCaseDisplayDriver.ParsePairs(AITestCaseDisplayDriver.FormatPairs(pairs)));
    }

    [Fact]
    public async Task Mock_ReturnsTheFixedResultInsteadOfRunningTheTool()
    {
        // Arrange
        var ran = false;
        var real = AIFunctionFactory.Create(() =>
        {
            ran = true;

            return "booked for real";
        }, "bookAppointment");
        var context = new AICompletionContext();
        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = new List<ToolRegistryEntry>
        {
            new() { Id = "bookAppointment", Name = "bookAppointment", Source = ToolRegistryEntrySource.Local, CreateAsync = _ => ValueTask.FromResult<AITool>(real) },
        };

        // Act
        AIConversationTools.Mock(context, new Dictionary<string, string> { ["BookAppointment"] = "{\"booked\":true}" });
        var entries = (IReadOnlyList<ToolRegistryEntry>)context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey];
        var tool = (AIFunction)await entries[0].CreateAsync(null);
        var result = await tool.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<MockedAIFunction>(tool);
        Assert.Equal("bookAppointment", tool.Name);
        Assert.Equal("{\"booked\":true}", result?.ToString());
        Assert.False(ran);
    }
}
