using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.Personas;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Agent.ProfileTemplates;
using CrestApps.OrchardCore.AI.Agent.ToolInstances;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The AI profile, template, persona and tool instance tools are registered with a title, a description and a category,
/// can be picked on a profile, ask for approval exactly where the tool coverage plan says so, and are registered by the
/// startup of the feature they belong to.
/// </summary>
public sealed class AIConfigurationToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { ListAIToolsTool.TheName, false },
        { SaveAIProfileTool.TheName, false },
        { SetAIProfileCapabilitiesTool.TheName, true },
        { CreateAIProfileFromTemplateTool.TheName, false },
        { DeleteAIProfileTool.TheName, true },
        { ListAIProfileVersionsTool.TheName, false },
        { RestoreAIProfileVersionTool.TheName, true },
        { TestAIProfileTool.TheName, true },
        { ListAIProfileTemplatesTool.TheName, false },
        { GetAIProfileTemplateTool.TheName, false },
        { SaveAIProfileTemplateTool.TheName, false },
        { DeleteAIProfileTemplateTool.TheName, true },
        { ListAIPersonasTool.TheName, false },
        { SaveAIPersonaTool.TheName, false },
        { DeleteAIPersonaTool.TheName, true },
        { ListAIToolInstancesTool.TheName, false },
        { SaveAIToolInstanceTool.TheName, true },
        { DeleteAIToolInstanceTool.TheName, true },
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

    [Theory]
    [InlineData(typeof(AIProfilesToolsStartup), 12, AIConstants.Feature.Area)]
    [InlineData(typeof(AIPersonasToolsStartup), 3, AIConstants.Feature.Personas)]
    [InlineData(typeof(AIToolInstancesToolsStartup), 3, AIConstants.Feature.ToolInstances)]
    public void EachFeatureStartup_RegistersItsToolsAndRequiresItsFeature(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var tools = GetTools(startupType);
        var attribute = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Contains(featureId, attribute.RequiredFeatureNames);
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
