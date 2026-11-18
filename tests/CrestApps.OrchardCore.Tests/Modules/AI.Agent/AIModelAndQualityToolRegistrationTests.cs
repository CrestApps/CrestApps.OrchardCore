using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.AIModels;
using CrestApps.OrchardCore.AI.Agent.Quality;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The provider connection, deployment, voice and AI quality tools are registered with a title, a description and a
/// category, can be picked on a profile, ask for approval exactly where the tool coverage plan says so, and are
/// registered by the startup of the feature they belong to.
/// </summary>
public sealed class AIModelAndQualityToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { ListAIProviderConnectionsTool.TheName, false },
        { SaveAIProviderConnectionTool.TheName, true },
        { DeleteAIProviderConnectionTool.TheName, true },
        { ListAIDeploymentsTool.TheName, false },
        { SaveAIDeploymentTool.TheName, true },
        { SetAIDeploymentPricingTool.TheName, false },
        { DeleteAIDeploymentTool.TheName, true },
        { ListAIVoicesTool.TheName, false },
        { CloneAIVoiceTool.TheName, true },
        { DeleteAIVoiceTool.TheName, true },
        { ListAITestRunsTool.TheName, false },
        { SaveAITestCaseTool.TheName, false },
        { DeleteAITestCasesTool.TheName, true },
        { SaveAIAlertRuleTool.TheName, false },
        { DeleteAIAlertRuleTool.TheName, true },
        { ListAIQACohortsTool.TheName, false },
        { SaveAIQACohortTool.TheName, true },
        { DeleteAIQACohortTool.TheName, true },
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
    [InlineData(typeof(AIProviderConnectionsToolsStartup), 3, AIConstants.Feature.ConnectionManagement)]
    [InlineData(typeof(AIDeploymentsToolsStartup), 7, AIConstants.Feature.Area)]
    [InlineData(typeof(AIQualityManagementToolsStartup), 8, AIConstants.Feature.Quality)]
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

    [Fact]
    public void ExistingQualityTools_AreLeftAsTheyWere()
    {
        // Act
        var tools = GetTools(typeof(QualityToolsStartup));

        // Assert
        Assert.Equal(4, tools.Count);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType = null)
    {
        var services = new ServiceCollection();

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
