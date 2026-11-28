using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements;
using CrestApps.OrchardCore.Omnichannel.Managements.AI;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The omnichannel tools are registered by the Omnichannel Management feature with a title, a description and a
/// category, ask for approval where the catalog says so, and refuse a caller who is not signed in or lacks the permission.
/// </summary>
public sealed class OmnichannelToolRegistrationTests
{
    private static readonly (string Name, bool RequiresApproval)[] _tools =
    [
        (ListOmnichannelActivitiesTool.TheName, false),
        (GetOmnichannelActivityTool.TheName, false),
        (CompleteOmnichannelActivityTool.TheName, false),
        (PurgeOmnichannelActivitiesTool.TheName, true),
        (ListActivityBatchesTool.TheName, false),
        (SaveActivityBatchTool.TheName, false),
        (PreviewActivityBatchTool.TheName, false),
        (LoadActivityBatchTool.TheName, true),
        (DeleteActivityBatchTool.TheName, true),
        (SaveOmnichannelCadenceTool.TheName, false),
        (DeleteOmnichannelRecordTool.TheName, true),
    ];

    public static TheoryData<string, bool> Tools
    {
        get
        {
            var data = new TheoryData<string, bool>();

            foreach (var (name, requiresApproval) in _tools)
            {
                data.Add(name, requiresApproval);
            }

            return data;
        }
    }

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var (name, _) in _tools)
            {
                data.Add(name);
            }

            return data;
        }
    }

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
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void Startup_RegistersOnlyTheseToolsAndBelongsToTheManagementFeature()
    {
        // Act
        var tools = GetTools();
        var required = typeof(AIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();
        var feature = typeof(AIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .Single();

        // Assert
        Assert.Equal(_tools.Length, tools.Count);
        Assert.Contains(AIConstants.Feature.Area, required.RequiredFeatureNames);
        Assert.Equal(OmnichannelConstants.Features.Managements, feature.FeatureName);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var harness = new OmnichannelToolHarness();
        harness.AddContact();
        harness.AddActivity("activity-1");
        var services = harness.Build(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(harness.UpdatedActivities);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName)
    {
        // Arrange
        var harness = new OmnichannelToolHarness();
        harness.AddContact();
        harness.AddActivity("activity-1");
        var services = harness.Build(authorize: (_, _) => false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(harness.UpdatedActivities);
        Assert.Empty(harness.Campaigns.Created);
        Assert.Empty(harness.Dispositions.Created);
        Assert.Empty(harness.Cadences.Created);
        Assert.Empty(harness.Batches.Created);
        Assert.Empty(harness.Batches.Deleted);
    }

    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["id"] = "activity-1",
            ["ids"] = new[] { "activity-1" },
            ["kind"] = "cadence",
            ["cloneFromId"] = "batch-1",
            ["displayText"] = "Something",
            ["name"] = "Something",
        };

    internal static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListOmnichannelActivitiesTool.TheName => new ListOmnichannelActivitiesTool(),
            GetOmnichannelActivityTool.TheName => new GetOmnichannelActivityTool(),
            CompleteOmnichannelActivityTool.TheName => new CompleteOmnichannelActivityTool(),
            PurgeOmnichannelActivitiesTool.TheName => new PurgeOmnichannelActivitiesTool(),
            ListActivityBatchesTool.TheName => new ListActivityBatchesTool(),
            SaveActivityBatchTool.TheName => new SaveActivityBatchTool(),
            PreviewActivityBatchTool.TheName => new PreviewActivityBatchTool(),
            LoadActivityBatchTool.TheName => new LoadActivityBatchTool(),
            DeleteActivityBatchTool.TheName => new DeleteActivityBatchTool(),
            SaveOmnichannelCadenceTool.TheName => new SaveOmnichannelCadenceTool(),
            _ => new DeleteOmnichannelRecordTool(),
        };

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools()
    {
        var services = new ServiceCollection();

        new AIToolsStartup(new PassThroughLocalizer<AIToolsStartup>()).ConfigureServices(services);

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
