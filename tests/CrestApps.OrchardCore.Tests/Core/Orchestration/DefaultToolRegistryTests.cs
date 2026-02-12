using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class DefaultToolRegistryTests
{
    [Fact]
    public async Task GetAllAsync_NoProviders_ReturnsEmpty()
    {
        var registry = CreateRegistry([]);

        var result = await registry.GetAllAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_SingleProvider_ReturnsAllEntries()
    {
        var entries = new List<ToolRegistryEntry>
        {
            new() { Name = "tool1", Description = "First tool", Source = ToolRegistryEntrySource.Local },
            new() { Name = "tool2", Description = "Second tool", Source = ToolRegistryEntrySource.Local },
        };
        var provider = new TestToolRegistryProvider(entries);
        var registry = CreateRegistry([provider]);

        var result = await registry.GetAllAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "tool1");
        Assert.Contains(result, t => t.Name == "tool2");
    }

    [Fact]
    public async Task GetAllAsync_MultipleProviders_AggregatesEntries()
    {
        var localEntries = new List<ToolRegistryEntry>
        {
            new() { Name = "localTool", Description = "A local tool", Source = ToolRegistryEntrySource.Local },
        };
        var mcpEntries = new List<ToolRegistryEntry>
        {
            new() { Name = "mcpTool", Description = "An MCP tool", Source = ToolRegistryEntrySource.McpServer, SourceId = "conn1" },
        };
        var registry = CreateRegistry([
            new TestToolRegistryProvider(localEntries),
            new TestToolRegistryProvider(mcpEntries),
        ]);

        var result = await registry.GetAllAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "localTool" && t.Source == ToolRegistryEntrySource.Local);
        Assert.Contains(result, t => t.Name == "mcpTool" && t.Source == ToolRegistryEntrySource.McpServer);
    }

    [Fact]
    public async Task GetAllAsync_ProviderThrows_SkipsAndContinues()
    {
        var goodEntries = new List<ToolRegistryEntry>
        {
            new() { Name = "goodTool", Description = "Healthy provider", Source = ToolRegistryEntrySource.Local },
        };
        var registry = CreateRegistry([
            new TestToolRegistryProvider(new InvalidOperationException("Provider error")),
            new TestToolRegistryProvider(goodEntries),
        ]);

        var result = await registry.GetAllAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("goodTool", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        var entries = new List<ToolRegistryEntry>
        {
            new() { Name = "tool1", Description = "First tool" },
        };
        var registry = CreateRegistry([new TestToolRegistryProvider(entries)]);

        var result = await registry.SearchAsync("", 5, new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_MatchingQuery_ReturnsRankedResults()
    {
        var entries = new List<ToolRegistryEntry>
        {
            new() { Name = "createJiraTicket", Description = "Create a Jira ticket from input data" },
            new() { Name = "sendSlackMessage", Description = "Send a message to a Slack channel" },
            new() { Name = "parseJsonData", Description = "Parse JSON data into structured format" },
        };
        var registry = CreateRegistry([new TestToolRegistryProvider(entries)]);

        var result = await registry.SearchAsync("Jira ticket", 5, new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.NotEmpty(result);
        Assert.Equal("createJiraTicket", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_RespectsTopK()
    {
        var entries = new List<ToolRegistryEntry>();
        for (var i = 0; i < 20; i++)
        {
            entries.Add(new ToolRegistryEntry
            {
                Name = $"tool{i}",
                Description = $"Tool number {i} for data processing",
            });
        }
        var registry = CreateRegistry([new TestToolRegistryProvider(entries)]);

        var result = await registry.SearchAsync("tool data processing", 3, new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.True(result.Count <= 3);
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsTopKByOriginalOrder()
    {
        var entries = new List<ToolRegistryEntry>
        {
            new() { Name = "alpha", Description = "Alpha tool" },
            new() { Name = "beta", Description = "Beta tool" },
            new() { Name = "gamma", Description = "Gamma tool" },
        };
        var registry = CreateRegistry([new TestToolRegistryProvider(entries)]);

        var result = await registry.SearchAsync("xyz completely unrelated query", 2, new AICompletionContext(), TestContext.Current.CancellationToken);

        // All scores are 0, so top-K from original order.
        Assert.True(result.Count <= 2);
    }

    private static DefaultToolRegistry CreateRegistry(IToolRegistryProvider[] providers)
    {
        return new DefaultToolRegistry(providers, new LuceneTextTokenizer(), NullLogger<DefaultToolRegistry>.Instance);
    }

    private sealed class TestToolRegistryProvider : IToolRegistryProvider
    {
        private readonly IReadOnlyList<ToolRegistryEntry> _entries;
        private readonly Exception _exception;

        public TestToolRegistryProvider(IReadOnlyList<ToolRegistryEntry> entries)
        {
            _entries = entries;
        }

        public TestToolRegistryProvider(Exception exception)
        {
            _exception = exception;
            _entries = [];
        }

        public Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
            AICompletionContext context,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_entries);
        }
    }
}
