using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class SystemToolRegistryProviderTests
{
    [Fact]
    public async Task GetToolsAsync_NoSystemTools_ReturnsEmpty()
    {
        var provider = CreateProvider([]);

        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetToolsAsync_OnlyNonSystemTools_ReturnsEmpty()
    {
        var options = new AIToolDefinitionOptions();
        options.SetTool("regular_tool", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "regular_tool",
            Title = "Regular Tool",
            Description = "A normal tool",
        });

        var provider = new SystemToolRegistryProvider(Options.Create(options));

        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetToolsAsync_SystemToolsReturned_WithCorrectSource()
    {
        var provider = CreateProvider(
        [
            ("search_documents", "Search Documents", "Search uploaded documents"),
            ("list_documents", "List Documents", "List available documents"),
        ]);

        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(ToolRegistryEntrySource.System, t.Source));
    }

    [Fact]
    public async Task GetToolsAsync_UsesDescriptionFromEntry()
    {
        var provider = CreateProvider(
        [
            ("search_documents", "Search Docs", "Perform vector search over uploaded documents"),
        ]);

        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("search_documents", result[0].Name);
        Assert.Equal("Perform vector search over uploaded documents", result[0].Description);
    }

    [Fact]
    public async Task GetToolsAsync_FallsBackToTitle_WhenDescriptionIsNull()
    {
        var options = new AIToolDefinitionOptions();
        options.SetTool("my_tool", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "my_tool",
            IsSystemTool = true,
            Title = "My Tool Title",
        });

        var provider = new SystemToolRegistryProvider(Options.Create(options));

        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Single(result);
        // Description is null, so it falls back to Title.
        Assert.Equal("My Tool Title", result[0].Description);
    }

    [Fact]
    public async Task GetToolsAsync_FiltersOutNonSystemTools()
    {
        var options = new AIToolDefinitionOptions();
        options.SetTool("system_tool", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "system_tool",
            IsSystemTool = true,
            Title = "System Tool",
            Description = "A system tool",
        });
        options.SetTool("regular_tool", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "regular_tool",
            Title = "Regular Tool",
            Description = "A regular tool",
        });

        var provider = new SystemToolRegistryProvider(Options.Create(options));

        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("system_tool", result[0].Name);
    }

    [Fact]
    public async Task GetToolsAsync_SourceIdIsNull()
    {
        var provider = CreateProvider([("tool1", "Tool 1", "Description")]);

        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Null(result[0].SourceId);
    }

    private static SystemToolRegistryProvider CreateProvider((string name, string title, string description)[] tools)
    {
        var options = new AIToolDefinitionOptions();
        foreach (var (name, title, description) in tools)
        {
            options.SetTool(name, new AIToolDefinitionEntry(typeof(AIFunction))
            {
                Name = name,
                IsSystemTool = true,
                Title = title,
                Description = description,
            });
        }

        return new SystemToolRegistryProvider(Options.Create(options));
    }
}
