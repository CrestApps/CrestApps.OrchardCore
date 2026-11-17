using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The workflow tools of the AI Agent module are registered with a title, a description and a category, can be picked
/// on a profile, and ask for approval exactly where the tool coverage plan says so. The tools this batch deferred are not
/// registered, and the existing workflow tools are left as they were.
/// </summary>
public sealed class OrchardWorkflowToolRegistrationTests
{
    private static readonly string[] _toolNames =
    [
        GetWorkflowActivityTool.TheName,
        SaveWorkflowTypeTool.TheName,
        ListWorkflowTypeVersionsTool.TheName,
        CloneWorkflowTypeTool.TheName,
        EnableWorkflowTypeTool.TheName,
        DisableWorkflowTypeTool.TheName,
        DeleteWorkflowTypeTool.TheName,
        ListWorkflowInstancesTool.TheName,
        GetWorkflowInstanceTool.TheName,
        RunWorkflowTypeTool.TheName,
        RerunWorkflowInstancesTool.TheName,
        CancelWorkflowInstancesTool.TheName,
        DeleteWorkflowInstancesTool.TheName,
        TriggerWorkflowSignalTool.TheName,
        CompleteWorkflowUserTaskTool.TheName,
    ];

    public static TheoryData<string, bool> Tools => new()
    {
        { GetWorkflowActivityTool.TheName, false },
        { SaveWorkflowTypeTool.TheName, true },
        { ListWorkflowTypeVersionsTool.TheName, false },
        { CloneWorkflowTypeTool.TheName, false },
        { EnableWorkflowTypeTool.TheName, false },
        { DisableWorkflowTypeTool.TheName, false },
        { DeleteWorkflowTypeTool.TheName, true },
        { ListWorkflowInstancesTool.TheName, false },
        { GetWorkflowInstanceTool.TheName, false },
        { RunWorkflowTypeTool.TheName, true },
        { RerunWorkflowInstancesTool.TheName, true },
        { CancelWorkflowInstancesTool.TheName, true },
        { DeleteWorkflowInstancesTool.TheName, true },
        { TriggerWorkflowSignalTool.TheName, true },
        { CompleteWorkflowUserTaskTool.TheName, true },
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
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.WorkflowsToolsStartup), 13)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.WorkflowsHttpToolsStartup), 1)]
    [InlineData(typeof(CrestApps.OrchardCore.AI.Agent.WorkflowUserTasksToolsStartup), 1)]
    public void EachFeatureStartup_RegistersItsTools(Type startupType, int expectedCount)
    {
        // Act
        var tools = GetTools(startupType);

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.All(tools.Keys, name => Assert.Contains(name, _toolNames));
    }

    [Theory]
    [InlineData("updateWorkflowDraft")]
    [InlineData("publishWorkflowType")]
    [InlineData("discardWorkflowDraft")]
    public void ADeferredTool_IsNotRegistered(string toolName)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var registered = tools.ContainsKey(toolName);

        // Assert
        Assert.False(registered);
    }

    [Theory]
    [InlineData("listWorkflowTypes", false)]
    [InlineData("getWorkflowType", false)]
    [InlineData("listWorkflowActivities", false)]
    [InlineData("createOrUpdateWorkflow", false)]
    public void AnExistingWorkflowTool_IsStillRegisteredAsBefore(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found);
        Assert.Equal(requiresApproval, tool.RequiresApproval);
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
