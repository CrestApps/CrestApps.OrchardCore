using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.BackgroundTasks;
using CrestApps.OrchardCore.AI.Agent.DataLocalization;
using CrestApps.OrchardCore.AI.Agent.HealthChecks;
using CrestApps.OrchardCore.AI.Agent.UrlRewriting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The Orchard Core site operations tools of the AI Agent module (background tasks, URL rewriting, health checks and
/// dynamic translations) are registered with a title, a description and a category, can be picked on a profile, and
/// ask for approval exactly where the tool coverage plan says so. The tools this batch deferred are not registered.
/// </summary>
public sealed class OrchardSiteOperationsToolRegistrationTests
{
    private static readonly string[] _toolNames =
    [
        ListBackgroundTasksTool.TheName,
        RunBackgroundTaskTool.TheName,
        ListUrlRewriteRulesTool.TheName,
        SaveUrlRewriteRuleTool.TheName,
        DeleteUrlRewriteRuleTool.TheName,
        GetSiteHealthTool.TheName,
        ListDynamicTranslationsTool.TheName,
        SaveDynamicTranslationsTool.TheName,
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { ListBackgroundTasksTool.TheName, false },
        { RunBackgroundTaskTool.TheName, false },
        { ListUrlRewriteRulesTool.TheName, false },
        { SaveUrlRewriteRuleTool.TheName, false },
        { DeleteUrlRewriteRuleTool.TheName, true },
        { GetSiteHealthTool.TheName, false },
        { ListDynamicTranslationsTool.TheName, false },
        { SaveDynamicTranslationsTool.TheName, false },
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
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.BackgroundTasksToolsStartup), 2)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.UrlRewritingToolsStartup), 3)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.HealthChecksToolsStartup), 1)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.DataLocalizationToolsStartup), 2)]
    public void EachFeatureStartup_RegistersItsTools(Type startupType, int expectedCount)
    {
        // Act
        var tools = GetTools(startupType);

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.All(tools.Keys, name => Assert.Contains(name, _toolNames));
    }

    [Theory]
    [InlineData("listRemoteInstancesAndClients")]
    [InlineData("saveRemoteInstance")]
    [InlineData("deleteRemoteInstance")]
    [InlineData("saveRemoteClient")]
    [InlineData("deleteRemoteClient")]
    [InlineData("exportDeploymentPlanToRemote")]
    [InlineData("saveBackgroundTaskSettings")]
    public void ADeferredTool_IsNotRegistered(string toolName)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var registered = tools.ContainsKey(toolName);

        // Assert
        Assert.False(registered);
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
