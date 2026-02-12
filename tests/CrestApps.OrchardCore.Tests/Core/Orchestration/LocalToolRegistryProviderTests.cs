using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class LocalToolRegistryProviderTests
{
    [Fact]
    public async Task GetToolsAsync_NullToolNames_ReturnsEmpty()
    {
        var provider = CreateProvider([]);

        var result = await provider.GetToolsAsync(new AICompletionContext { ToolNames = null }, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetToolsAsync_EmptyToolNames_ReturnsEmpty()
    {
        var provider = CreateProvider([]);

        var result = await provider.GetToolsAsync(new AICompletionContext { ToolNames = [] }, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetToolsAsync_MatchingToolNames_ReturnsEntries()
    {
        var provider = CreateProvider(
        [
            ("tool1", "Tool 1", "First tool"),
            ("tool2", "Tool 2", "Second tool"),
            ("tool3", "Tool 3", "Third tool"),
        ]);

        var result = await provider.GetToolsAsync(new AICompletionContext { ToolNames = ["tool1", "tool3"] }, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "tool1" && t.Description == "First tool");
        Assert.Contains(result, t => t.Name == "tool3" && t.Description == "Third tool");
        Assert.All(result, t => Assert.Equal(ToolRegistryEntrySource.Local, t.Source));
    }

    [Fact]
    public async Task GetToolsAsync_UnknownToolNames_SkipsMissing()
    {
        var provider = CreateProvider([("tool1", "Tool 1", "First tool")]);

        var result = await provider.GetToolsAsync(new AICompletionContext { ToolNames = ["tool1", "nonexistent"] }, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("tool1", result[0].Name);
    }

    [Fact]
    public async Task GetToolsAsync_FallsBackToTitle_WhenDescriptionIsNull()
    {
        var provider = CreateProvider([("tool1", "My Tool Title", null)]);

        var result = await provider.GetToolsAsync(new AICompletionContext { ToolNames = ["tool1"] }, TestContext.Current.CancellationToken);

        Assert.Single(result);
        // When Description is null, falls back to Title.
        Assert.Equal("My Tool Title", result[0].Description);
    }

    [Fact]
    public async Task GetToolsAsync_FallsBackToName_WhenTitleAndDescriptionAreNull()
    {
        var provider = CreateProvider([("tool1", null, null)]);

        var result = await provider.GetToolsAsync(new AICompletionContext { ToolNames = ["tool1"] }, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("tool1", result[0].Description);
    }

    private static LocalToolRegistryProvider CreateProvider((string name, string title, string description)[] tools)
    {
        var options = new AIToolDefinitionOptions();
        foreach (var (name, title, description) in tools)
        {
            options.SetTool(name, new AIToolDefinitionEntry(typeof(Microsoft.Extensions.AI.AIFunction))
            {
                Name = name,
                Title = title,
                Description = description,
            });
        }

        return new LocalToolRegistryProvider(Options.Create(options));
    }
}
