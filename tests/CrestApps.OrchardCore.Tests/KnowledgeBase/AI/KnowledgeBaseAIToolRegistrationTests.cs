using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using KnowledgeBaseAIToolsStartup = CrestApps.OrchardCore.KnowledgeBase.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase.AI;

/// <summary>
/// The Knowledge Base AI tools are registered by the Knowledge Base feature when the AI feature is on, with a title, a
/// description and the Knowledge Base category, selectable, and without approval: they only read.
/// </summary>
public sealed class KnowledgeBaseAIToolRegistrationTests
{
    [Theory]
    [InlineData(SearchKnowledgeBaseTool.TheName)]
    [InlineData(GetKnowledgeArticleTool.TheName)]
    public void Tool_IsRegisteredWithItsTexts_AndAsksForNoApproval(string toolName)
    {
        // Act
        var found = GetTools().TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Knowledge Base", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.False(tool.RequiresApproval);
    }

    [Fact]
    public void TheStartup_RegistersOnlyTheTwoTools_ForTheKnowledgeBaseFeature_WhenAIIsOn()
    {
        // Act
        var tools = GetTools();
        var required = typeof(KnowledgeBaseAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(KnowledgeBaseAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        // Assert
        Assert.Equal([GetKnowledgeArticleTool.TheName, SearchKnowledgeBaseTool.TheName], tools.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(SearchKnowledgeBaseTool.TheName, new SearchKnowledgeBaseTool().Name);
        Assert.Equal(GetKnowledgeArticleTool.TheName, new GetKnowledgeArticleTool().Name);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(KnowledgeBaseConstants.Feature, feature.FeatureName);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools()
    {
        var services = new ServiceCollection();

        new KnowledgeBaseAIToolsStartup(new PassThroughLocalizer()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<KnowledgeBaseAIToolsStartup>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
