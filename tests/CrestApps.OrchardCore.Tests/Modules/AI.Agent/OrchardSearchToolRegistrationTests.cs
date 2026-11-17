using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Indexing;
using CrestApps.OrchardCore.AI.Agent.Queries;
using CrestApps.OrchardCore.AI.Agent.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The Orchard Core tools of the AI Agent module for search indexes, searches and queries are registered with a title,
/// a description and a category, can be picked on a profile, and ask for approval exactly where the tool coverage plan
/// says so.
/// </summary>
public sealed class OrchardSearchToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { ListIndexProfilesTool.TheName, false },
        { GetIndexProfileTool.TheName, false },
        { SaveIndexProfileTool.TheName, false },
        { SynchronizeIndexProfileTool.TheName, false },
        { RebuildIndexProfileTool.TheName, true },
        { DeleteIndexProfileTool.TheName, true },
        { SearchIndexTool.TheName, false },
        { RunSearchQueryTool.TheName, false },
        { ListQueriesTool.TheName, false },
        { SaveQueryTool.TheName, true },
        { DeleteQueryTool.TheName, true },
        { RunQueryTool.TheName, false },
        { RunSqlQueryTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.NotEqual(toolName, tool.Description);
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void RunSearchQuery_IsRegisteredForLuceneAndForElasticsearch_WithTheSameTexts()
    {
        // Arrange
        var lucene = GetTools(typeof(CrestApps.OrchardCore.AI.Agent.LuceneToolsStartup));
        var elasticsearch = GetTools(typeof(CrestApps.OrchardCore.AI.Agent.ElasticsearchToolsStartup));

        // Act
        var luceneTool = lucene[RunSearchQueryTool.TheName];
        var elasticsearchTool = elasticsearch[RunSearchQueryTool.TheName];

        // Assert
        Assert.Equal(luceneTool.Title, elasticsearchTool.Title);
        Assert.Equal(luceneTool.Description, elasticsearchTool.Description);
        Assert.Equal(luceneTool.Category, elasticsearchTool.Category);
        Assert.Equal(luceneTool.RequiresApproval, elasticsearchTool.RequiresApproval);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType = null)
    {
        var services = new ServiceCollection();

        // The tools are registered by the module's feature startups; run them all, or the one that is asked for.
        foreach (var type in typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly.GetTypes()
            .Where(type => typeof(StartupBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => startupType is null || type == startupType))
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
