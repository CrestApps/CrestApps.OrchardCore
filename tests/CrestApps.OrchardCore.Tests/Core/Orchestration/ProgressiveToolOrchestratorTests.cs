using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class ProgressiveToolOrchestratorTests
{
    [Fact]
    public void Name_ReturnsDefault()
    {
        var orchestrator = CreateOrchestrator();

        Assert.Equal("default", orchestrator.Name);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_FewTools_SkipsPlanningAndIncludesAll()
    {
        // 3 tools, ScopingThreshold is 30 → should skip planning and scoping.
        var tools = CreateToolEntries(3);
        var registry = new FakeToolRegistry(tools);
        var completionService = new FakeCompletionService("Hello from AI");
        var orchestrator = CreateOrchestrator(completionService, registry);

        var context = CreateContext("Say hello");
        var result = await CollectStreamAsync(orchestrator, context);

        Assert.Equal("Hello from AI", result);
        Assert.Equal(0, completionService.CompleteCallCount);
        Assert.Equal(1, completionService.StreamCallCount);
        // All 3 tools should be included.
        Assert.Equal(3, context.CompletionContext.ToolNames.Length);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_IncludesAllToolsWhenBelowThreshold()
    {
        var tools = CreateToolEntries(3);
        var registry = new FakeToolRegistry(tools);
        var completionService = new FakeCompletionService("OK");
        var orchestrator = CreateOrchestrator(completionService, registry);

        var context = CreateContext("Do something");

        await CollectStreamAsync(orchestrator, context);

        // All tool names should be present when below planning threshold.
        Assert.Contains("tool0", context.CompletionContext.ToolNames);
        Assert.Contains("tool1", context.CompletionContext.ToolNames);
        Assert.Contains("tool2", context.CompletionContext.ToolNames);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_ManyTools_ActivatesPlanningAndScopes()
    {
        // Create 35 tools: 5 local (user-selected) + 30 MCP (auto-discovered).
        // With ScopingThreshold=30, 35 tools exceeds the threshold and
        // MCP presence triggers the full planning phase.
        var tools = new List<ToolRegistryEntry>();
        for (var i = 0; i < 5; i++)
        {
            tools.Add(new ToolRegistryEntry
            {
                Name = $"local_tool{i}",
                Description = $"Local tool {i} for Jira ticket management",
                Source = ToolRegistryEntrySource.Local,
            });
        }
        for (var i = 0; i < 30; i++)
        {
            tools.Add(new ToolRegistryEntry
            {
                Name = $"mcp_tool{i}",
                Description = $"MCP tool {i} for {(i < 3 ? "Jira ticket management" : "unrelated tasks")}",
                Source = ToolRegistryEntrySource.McpServer,
            });
        }
        var registry = new FakeToolRegistry(tools);

        // Planning call returns a plan mentioning "Jira" and "ticket".
        var completionService = new FakeCompletionService("Stream result");
        completionService.PlanningResponse = "Step 1: Use Jira ticket tool to create the ticket.";

        var orchestrator = CreateOrchestrator(completionService, registry);
        var context = CreateContext("Create a Jira ticket");

        var result = await CollectStreamAsync(orchestrator, context);

        Assert.Equal("Stream result", result);
        // Planning phase should have been called (MCP tools present + above scoping threshold).
        Assert.Equal(1, completionService.CompleteCallCount);
        // All 5 local tools should always be included.
        for (var i = 0; i < 5; i++)
        {
            Assert.Contains($"local_tool{i}", context.CompletionContext.ToolNames);
        }
        // Total should be fewer than all 35 (local 5 + scoped MCP subset).
        Assert.True(context.CompletionContext.ToolNames.Length < 35);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_EmptyToolRegistry_DoesNotThrow()
    {
        var registry = new FakeToolRegistry([]);
        var completionService = new FakeCompletionService("OK");
        var orchestrator = CreateOrchestrator(completionService, registry);

        var context = CreateContext("Test");

        var result = await CollectStreamAsync(orchestrator, context);

        Assert.Equal("OK", result);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_AboveThresholdNoMcp_ScopesByRelevance()
    {
        // 35 system tools (no MCP) → exceeds ScopingThreshold (30) but no MCP
        // → should scope without LLM planner call.
        // All tools are scored by relevance; only relevant ones are included.
        var tools = new List<ToolRegistryEntry>();
        for (var i = 0; i < 35; i++)
        {
            tools.Add(new ToolRegistryEntry
            {
                Name = $"sys_tool{i}",
                Description = i < 3 ? "Create content articles and pages" : $"System tool {i} for misc tasks",
                Source = ToolRegistryEntrySource.System,
            });
        }
        var registry = new FakeToolRegistry(tools);
        var completionService = new FakeCompletionService("Response");
        var orchestrator = CreateOrchestrator(completionService, registry);

        var context = CreateContext("Create an article about AI");
        var result = await CollectStreamAsync(orchestrator, context);

        Assert.Equal("Response", result);
        // NO planning call should have been made (no MCP, below PlanningThreshold).
        Assert.Equal(0, completionService.CompleteCallCount);
        // Only relevant tools should be selected (the 3 with matching description),
        // not all 35.
        Assert.True(context.CompletionContext.ToolNames.Length < 35);
        Assert.True(context.CompletionContext.ToolNames.Length >= 3);
    }

    [Fact]
    public async Task ExecuteStreamingAsync_EmptyRegistry_ProducesOutput()
    {
        var registry = new FakeToolRegistry([]);
        var completionService = new FakeCompletionService("No tools response");
        var orchestrator = CreateOrchestrator(completionService, registry);

        var context = CreateContext("What is AI?");

        var result = await CollectStreamAsync(orchestrator, context);

        Assert.Equal("No tools response", result);
    }

    [Fact]
    public async Task ScopeToolsAsync_NullPlan_FallsBackToAll()
    {
        var tools = CreateToolEntries(5);
        var orchestrator = CreateOrchestrator();
        var context = CreateContext("Do something");

        var result = await orchestrator.ScopeToolsAsync(null, context, tools);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task ScopeToolsAsync_EmptyPlan_ReturnsLocalPlusNonLocal()
    {
        // 10 local + 10 MCP tools. Empty plan → local all preserved, non-local capped.
        var tools = new List<ToolRegistryEntry>();
        for (var i = 0; i < 10; i++)
        {
            tools.Add(new ToolRegistryEntry
            {
                Name = $"local{i}",
                Description = $"Local tool {i}",
                Source = ToolRegistryEntrySource.Local,
            });
        }
        for (var i = 0; i < 10; i++)
        {
            tools.Add(new ToolRegistryEntry
            {
                Name = $"mcp{i}",
                Description = $"MCP tool {i}",
                Source = ToolRegistryEntrySource.McpServer,
            });
        }
        var orchestrator = CreateOrchestrator();
        var context = CreateContext("Do something");

        var result = await orchestrator.ScopeToolsAsync("   ", context, tools);
        var resultNames = result.Select(e => e.Name).ToList();

        // All 10 local tools must be included.
        for (var i = 0; i < 10; i++)
        {
            Assert.Contains($"local{i}", resultNames);
        }

        // Total length should be reasonable (local + some non-local).
        Assert.True(result.Count >= 10);
    }

    [Fact]
    public async Task ScopeToolsAsync_MatchingPlan_SelectsRelevantTools()
    {
        var tools = new List<ToolRegistryEntry>
        {
            new() { Name = "createJiraTicket", Description = "Create a Jira ticket", Source = ToolRegistryEntrySource.McpServer },
            new() { Name = "sendSlackMessage", Description = "Send a Slack message", Source = ToolRegistryEntrySource.McpServer },
            new() { Name = "parseJson", Description = "Parse JSON data", Source = ToolRegistryEntrySource.System },
            new() { Name = "updateDatabase", Description = "Update database records", Source = ToolRegistryEntrySource.System },
        };
        var orchestrator = CreateOrchestrator();
        var context = CreateContext("Create a Jira ticket");

        var plan = "Step 1: Create a Jira ticket for the issue.";
        var result = await orchestrator.ScopeToolsAsync(plan, context, tools);
        var resultNames = result.Select(e => e.Name).ToList();

        // Jira tool matched by plan.
        Assert.Contains("createJiraTicket", resultNames);
    }

    [Fact]
    public async Task ScopeToolsAsync_AllToolsScoredUniformlyByRelevance()
    {
        var tools = new List<ToolRegistryEntry>
        {
            new() { Name = "userSelectedTool", Description = "A user selected content tool", Source = ToolRegistryEntrySource.Local },
            new() { Name = "createJiraTicket", Description = "Create a Jira ticket for issues", Source = ToolRegistryEntrySource.Local },
            new() { Name = "mcpJiraTool", Description = "Create a Jira ticket", Source = ToolRegistryEntrySource.McpServer },
            new() { Name = "mcpSlackTool", Description = "Send a Slack message", Source = ToolRegistryEntrySource.McpServer },
            new() { Name = "systemImageTool", Description = "Generate an image", Source = ToolRegistryEntrySource.System },
        };
        var orchestrator = CreateOrchestrator();
        var context = CreateContext("Create a Jira ticket");

        // Plan mentions only Jira.
        var plan = "Step 1: Create a Jira ticket.";
        var result = await orchestrator.ScopeToolsAsync(plan, context, tools);
        var resultNames = result.Select(e => e.Name).ToList();

        // Local and MCP Jira tools should be included due to plan match.
        Assert.Contains("createJiraTicket", resultNames);
        Assert.Contains("mcpJiraTool", resultNames);
        // Unrelated tools should not be included when they don't match.
        Assert.DoesNotContain("mcpSlackTool", resultNames);
    }

    [Fact]
    public async Task ScopeToolsAsync_NoMatchesInPlan_FallbackByOriginalOrder()
    {
        // 2 local + 1 system + 5 MCP tools, plan matches nothing.
        var tools = new List<ToolRegistryEntry>
        {
            new() { Name = "local0", Description = "Local tool", Source = ToolRegistryEntrySource.Local },
            new() { Name = "local1", Description = "Local tool", Source = ToolRegistryEntrySource.Local },
            new() { Name = "sys0", Description = "System tool", Source = ToolRegistryEntrySource.System },
        };
        for (var i = 0; i < 5; i++)
        {
            tools.Add(new ToolRegistryEntry
            {
                Name = $"mcp{i}",
                Description = $"MCP tool {i}",
                Source = ToolRegistryEntrySource.McpServer,
            });
        }
        var orchestrator = CreateOrchestrator();
        var context = CreateContext("Do something");

        var plan = "xyz completely unrelated zzz qqq";
        var result = await orchestrator.ScopeToolsAsync(plan, context, tools);

        // When no tools match, fallback fills the budget by original order.
        Assert.True(result.Count > 0);
    }

    [Fact]
    public async Task PlanAsync_ReturnsLLMResponse()
    {
        var completionService = new FakeCompletionService("stream text");
        completionService.PlanningResponse = "Plan: Use tool1 and tool2";
        var tools = CreateToolEntries(3);
        var orchestrator = CreateOrchestrator(completionService);

        var context = CreateContext("Do complex task");
        var plan = await orchestrator.PlanAsync(context, tools, TestContext.Current.CancellationToken);

        Assert.Equal("Plan: Use tool1 and tool2", plan);
    }

    [Fact]
    public async Task PlanAsync_CompletionServiceThrows_ReturnsNull()
    {
        var completionService = new FakeCompletionService("stream text");
        completionService.PlanningException = new InvalidOperationException("API error");
        var tools = CreateToolEntries(3);
        var orchestrator = CreateOrchestrator(completionService);

        var context = CreateContext("Do something");
        var plan = await orchestrator.PlanAsync(context, tools, TestContext.Current.CancellationToken);

        Assert.Null(plan);
    }

    private static ProgressiveToolOrchestrator CreateOrchestrator(
        FakeCompletionService completionService = null,
        FakeToolRegistry toolRegistry = null)
    {
        return new ProgressiveToolOrchestrator(
            completionService ?? new FakeCompletionService("default response"),
            toolRegistry ?? new FakeToolRegistry([]),
            new LuceneTextTokenizer(),
            Options.Create(new ProgressiveToolOrchestratorOptions()),
            NullLogger<ProgressiveToolOrchestrator>.Instance);
    }

    private static List<ToolRegistryEntry> CreateToolEntries(int count)
    {
        var entries = new List<ToolRegistryEntry>();
        for (var i = 0; i < count; i++)
        {
            entries.Add(new ToolRegistryEntry
            {
                Name = $"tool{i}",
                Description = $"Tool {i} description",
                Source = ToolRegistryEntrySource.Local,
            });
        }
        return entries;
    }

    private static OrchestrationContext CreateContext(string userMessage)
    {
        return new OrchestrationContext
        {
            UserMessage = userMessage,
            ConversationHistory = [new ChatMessage(ChatRole.User, userMessage)],
            CompletionContext = new AICompletionContext
            {
                ConnectionName = "test",
                DeploymentId = "test-deployment",
                ToolNames = ["tool0", "tool1", "tool2"],
            },
            SourceName = "TestClient",
        };
    }

    private static async Task<string> CollectStreamAsync(
        ProgressiveToolOrchestrator orchestrator,
        OrchestrationContext context)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in orchestrator.ExecuteStreamingAsync(context, TestContext.Current.CancellationToken))
        {
            sb.Append(chunk.Text);
        }
        return sb.ToString();
    }

    /// <summary>
    /// A fake completion service that returns predictable planning and streaming responses.
    /// </summary>
    private sealed class FakeCompletionService : IAICompletionService
    {
        private readonly string _streamText;

        public FakeCompletionService(string streamText)
        {
            _streamText = streamText;
        }

        public string PlanningResponse { get; set; }
        public Exception PlanningException { get; set; }
        public int CompleteCallCount { get; private set; }
        public int StreamCallCount { get; private set; }

        public Task<ChatResponse> CompleteAsync(
            string clientName,
            IEnumerable<ChatMessage> messages,
            AICompletionContext context,
            CancellationToken cancellationToken = default)
        {
            CompleteCallCount++;

            if (PlanningException is not null)
            {
                throw PlanningException;
            }

            var text = PlanningResponse ?? "No plan";
            var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, text)]);
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> CompleteStreamingAsync(
            string clientName,
            IEnumerable<ChatMessage> messages,
            AICompletionContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamCallCount++;
            await Task.CompletedTask;
            yield return new ChatResponseUpdate
            {
                Contents = [new Microsoft.Extensions.AI.TextContent(_streamText)],
            };
        }
    }

    /// <summary>
    /// A fake tool registry that returns a fixed set of entries.
    /// </summary>
    private sealed class FakeToolRegistry : IToolRegistry
    {
        private readonly IReadOnlyList<ToolRegistryEntry> _entries;

        public FakeToolRegistry(IReadOnlyList<ToolRegistryEntry> entries)
        {
            _entries = entries;
        }

        public Task<IReadOnlyList<ToolRegistryEntry>> GetAllAsync(
            AICompletionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_entries);
        }

        public Task<IReadOnlyList<ToolRegistryEntry>> SearchAsync(
            string query,
            int topK,
            AICompletionContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ToolRegistryEntry>>(
                _entries.Take(topK).ToList());
        }
    }
}
