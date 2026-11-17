using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.DataPipelines;
using CrestApps.OrchardCore.AI.Agent.SharedLinks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The data pipeline and shared link tools of the AI Agent module are registered with a title, a description and a
/// category, can be picked on a profile, and ask for approval exactly where the tool coverage plan says so. The tools
/// this batch deferred are not registered, and the mirrored permissions keep the names and implications of the Data
/// Pipelines module.
/// </summary>
public sealed class OrchardDataPipelineToolRegistrationTests
{
    private static readonly string[] _toolNames =
    [
        ListDataPipelineStepTypesTool.TheName,
        ListDataSetsTool.TheName,
        GetDataSetSchemaTool.TheName,
        ListSharedLinksTool.TheName,
        RevokeSharedLinkTool.TheName,
        DeleteSharedLinkTool.TheName,
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListDataPipelineStepTypesTool.TheName, false },
        { ListDataSetsTool.TheName, false },
        { GetDataSetSchemaTool.TheName, false },
        { ListSharedLinksTool.TheName, false },
        { RevokeSharedLinkTool.TheName, true },
        { DeleteSharedLinkTool.TheName, true },
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
    [InlineData(typeof(DataPipelinesToolsStartup), 3)]
    [InlineData(typeof(DataPipelinesCoreToolsStartup), 3)]
    public void EachFeatureStartup_RegistersItsTools(Type startupType, int expectedCount)
    {
        // Act
        var tools = GetTools(startupType);

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.All(tools.Keys, name => Assert.Contains(name, _toolNames));
    }

    [Theory]
    [InlineData(typeof(DataPipelinesToolsStartup), "OrchardCore.DataPipelines")]
    [InlineData(typeof(DataPipelinesCoreToolsStartup), "OrchardCore.DataPipelines.Core")]
    public void EachFeatureStartup_RequiresItsFeature(Type startupType, string featureId)
    {
        // Act
        var attribute = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();

        // Assert
        Assert.Contains(featureId, attribute.RequiredFeatureNames);
    }

    [Theory]
    [InlineData("listDataPipelines")]
    [InlineData("getDataPipeline")]
    [InlineData("saveDataPipeline")]
    [InlineData("updateDataPipelineDraft")]
    [InlineData("previewDataPipelineStep")]
    [InlineData("publishDataPipeline")]
    [InlineData("discardDataPipelineDraft")]
    [InlineData("enableDataPipeline")]
    [InlineData("disableDataPipeline")]
    [InlineData("resetDataPipelineState")]
    [InlineData("deleteDataPipeline")]
    [InlineData("runDataPipeline")]
    [InlineData("listDataPipelineRuns")]
    [InlineData("getDataPipelineRun")]
    [InlineData("cancelDataPipelineRun")]
    public void ADeferredTool_IsNotRegistered(string toolName)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var registered = tools.ContainsKey(toolName);

        // Assert
        Assert.False(registered);
    }

    [Fact]
    public void MirroredPermissions_KeepTheNamesAndImplicationsOfTheDataPipelinesModule()
    {
        // Assert
        Assert.Equal("ManageDataPipelines", OrchardCorePermissions.ManageDataPipelines.Name);
        Assert.True(OrchardCorePermissions.ManageDataPipelines.IsSecurityCritical);
        Assert.Equal("ManageSharedLinks", OrchardCorePermissions.ManageSharedLinks.Name);
        Assert.Equal("ViewDataPipelines", OrchardCorePermissions.ViewDataPipelines.Name);
        Assert.Equal(["ManageDataPipelines", "RunDataPipelines"], OrchardCorePermissions.ViewDataPipelines.ImpliedBy.Select(permission => permission.Name));
        Assert.Equal(["ManageDataPipelines"], OrchardCorePermissions.RunDataPipelines.ImpliedBy.Select(permission => permission.Name));
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
