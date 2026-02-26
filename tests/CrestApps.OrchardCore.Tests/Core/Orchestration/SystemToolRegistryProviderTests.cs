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
            ("tool_a", "Tool A", "First tool"),
            ("tool_b", "Tool B", "Second tool"),
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
            ("my_tool", "My Tool", "Perform vector search over uploaded documents"),
        ]);

        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("my_tool", result[0].Name);
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

    [Fact]
    public async Task GetToolsAsync_DataSourceSearchTool_ExcludedWhenNoDataSource()
    {
        var provider = CreateProviderWithPurpose(
            ("search_data_sources", "Search Data Sources", "Search data sources", AIToolPurposes.DataSourceSearch));

        var context = new AICompletionContext();

        var result = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetToolsAsync_DataSourceSearchTool_IncludedWhenDataSourceSet()
    {
        var provider = CreateProviderWithPurpose(
            ("search_data_sources", "Search Data Sources", "Search data sources", AIToolPurposes.DataSourceSearch));

        var context = new AICompletionContext { DataSourceId = "ds-123" };

        var result = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("search_data_sources", result[0].Name);
    }

    [Fact]
    public async Task GetToolsAsync_DocumentProcessingTool_ExcludedWhenNoDocuments()
    {
        var provider = CreateProviderWithPurpose(
            ("search_documents", "Search Docs", "Search uploaded documents", AIToolPurposes.DocumentProcessing));

        var context = new AICompletionContext();

        var result = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetToolsAsync_DocumentProcessingTool_IncludedWhenHasDocumentsSet()
    {
        var provider = CreateProviderWithPurpose(
            ("search_documents", "Search Docs", "Search uploaded documents", AIToolPurposes.DocumentProcessing));

        var context = new AICompletionContext();
        context.AdditionalProperties[AICompletionContextKeys.HasDocuments] = true;

        var result = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("search_documents", result[0].Name);
    }

    [Fact]
    public async Task GetToolsAsync_MixedTools_OnlyIncludesAvailableOnes()
    {
        var options = new AIToolDefinitionOptions();
        options.SetTool("search_data_sources", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "search_data_sources",
            IsSystemTool = true,
            Description = "Search data sources",
            Purpose = AIToolPurposes.DataSourceSearch,
        });
        options.SetTool("search_documents", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "search_documents",
            IsSystemTool = true,
            Description = "Search uploaded documents",
            Purpose = AIToolPurposes.DocumentProcessing,
        });
        options.SetTool("generate_image", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "generate_image",
            IsSystemTool = true,
            Description = "Generate an image",
            Purpose = AIToolPurposes.ContentGeneration,
        });

        var provider = new SystemToolRegistryProvider(Options.Create(options));

        // No data source, no documents — only ungated tools should appear.
        var context = new AICompletionContext();
        var result = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("generate_image", result[0].Name);
    }

    [Fact]
    public async Task GetToolsAsync_AllContextAvailable_IncludesAllSystemTools()
    {
        var options = new AIToolDefinitionOptions();
        options.SetTool("search_data_sources", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "search_data_sources",
            IsSystemTool = true,
            Description = "Search data sources",
            Purpose = AIToolPurposes.DataSourceSearch,
        });
        options.SetTool("search_documents", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "search_documents",
            IsSystemTool = true,
            Description = "Search uploaded documents",
            Purpose = AIToolPurposes.DocumentProcessing,
        });
        options.SetTool("generate_image", new AIToolDefinitionEntry(typeof(AIFunction))
        {
            Name = "generate_image",
            IsSystemTool = true,
            Description = "Generate an image",
            Purpose = AIToolPurposes.ContentGeneration,
        });

        var provider = new SystemToolRegistryProvider(Options.Create(options));

        // Both data source and documents available — all tools should appear.
        var context = new AICompletionContext { DataSourceId = "ds-123" };
        context.AdditionalProperties[AICompletionContextKeys.HasDocuments] = true;

        var result = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, t => t.Name == "search_data_sources");
        Assert.Contains(result, t => t.Name == "search_documents");
        Assert.Contains(result, t => t.Name == "generate_image");
    }

    [Fact]
    public async Task GetToolsAsync_NoPurpose_AlwaysIncluded()
    {
        var provider = CreateProvider([("generic_tool", "Generic Tool", "A generic system tool")]);

        // Tools without a purpose are always included regardless of context.
        var result = await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("generic_tool", result[0].Name);
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

    private static SystemToolRegistryProvider CreateProviderWithPurpose(
        params (string name, string title, string description, string purpose)[] tools)
    {
        var options = new AIToolDefinitionOptions();
        foreach (var (name, title, description, purpose) in tools)
        {
            options.SetTool(name, new AIToolDefinitionEntry(typeof(AIFunction))
            {
                Name = name,
                IsSystemTool = true,
                Title = title,
                Description = description,
                Purpose = purpose,
            });
        }

        return new SystemToolRegistryProvider(Options.Create(options));
    }
}
