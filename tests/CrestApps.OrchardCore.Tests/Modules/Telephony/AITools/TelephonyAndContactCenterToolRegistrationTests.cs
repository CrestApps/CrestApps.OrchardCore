using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.AI;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.AI;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony.AITools;

/// <summary>
/// The tools of the last CRM and contact center batch (the agent's own workspace, interval statistics, health,
/// dialing from the soft phone, Telnyx numbers and local do-not-call lists) are registered by the feature of the
/// page they mirror, with a title, a description and a category, and ask for approval where the batch says so.
/// </summary>
public sealed class TelephonyAndContactCenterToolRegistrationTests
{
    private static readonly (string Name, bool RequiresApproval, Type Startup, string Category)[] _tools =
    [
        (GetMyAgentWorkStateTool.TheName, false, typeof(AgentWorkspaceAIToolsStartup), "Contact Center"),
        (SetContactCenterAgentStateTool.TheName, false, typeof(AgentWorkspaceAIToolsStartup), "Contact Center"),
        (GetIntervalStatisticsStatusTool.TheName, false, typeof(IntervalStatisticsAIToolsStartup), "Contact Center"),
        (RebuildIntervalStatisticsTool.TheName, false, typeof(IntervalStatisticsAIToolsStartup), "Contact Center"),
        (GetContactCenterHealthTool.TheName, false, typeof(HealthAIToolsStartup), "Contact Center"),
        (DialFromSoftPhoneTool.TheName, true, typeof(SoftPhoneAIToolsStartup), "Telephony"),
        (GetTelnyxConnectionStatusTool.TheName, false, typeof(CrestApps.OrchardCore.Telnyx.AIToolsStartup), "Telephony"),
        (ListTelnyxNumbersTool.TheName, false, typeof(CrestApps.OrchardCore.Telnyx.AIToolsStartup), "Telephony"),
        (SearchTelnyxAvailableNumbersTool.TheName, false, typeof(CrestApps.OrchardCore.Telnyx.AIToolsStartup), "Telephony"),
        (SetTelnyxNumberCallerNameTool.TheName, true, typeof(CrestApps.OrchardCore.Telnyx.AIToolsStartup), "Telephony"),
        (ListLocalDncListsTool.TheName, false, typeof(CrestApps.OrchardCore.DncRegistry.AIToolsStartup), "Telephony"),
        (ImportLocalDncListTool.TheName, true, typeof(CrestApps.OrchardCore.DncRegistry.AIToolsStartup), "Telephony"),
        (SetLocalDncListImportStateTool.TheName, true, typeof(CrestApps.OrchardCore.DncRegistry.AIToolsStartup), "Telephony"),
        (DeleteLocalDncListTool.TheName, true, typeof(CrestApps.OrchardCore.DncRegistry.AIToolsStartup), "Telephony"),
    ];

    private static readonly Dictionary<Type, (string Feature, string[] Required)> _features = new()
    {
        [typeof(AgentWorkspaceAIToolsStartup)] = (ContactCenterConstants.Feature.Agents, [AIFeatureIds.Area]),
        [typeof(IntervalStatisticsAIToolsStartup)] = (ContactCenterConstants.Feature.IntervalStatistics, [AIFeatureIds.Area]),
        [typeof(HealthAIToolsStartup)] = (ContactCenterConstants.Feature.Area, [AIFeatureIds.Area, OrchardCoreFeatureIds.HealthChecks]),
        [typeof(CrestApps.OrchardCore.Telephony.AIToolsStartup)] = (TelephonyConstants.Feature.Area, [AIFeatureIds.Area]),
        [typeof(SoftPhoneAIToolsStartup)] = (TelephonyConstants.Feature.SoftPhoneCore, [AIFeatureIds.Area]),
        [typeof(CrestApps.OrchardCore.Telnyx.AIToolsStartup)] = (TelnyxConstants.Feature.Area, [AIConstants.Feature.Area]),
        [typeof(CrestApps.OrchardCore.DncRegistry.AIToolsStartup)] = (DncRegistryConstants.Features.Local, [AIFeatureIds.Area]),
    };

    public static TheoryData<string, bool> Tools
    {
        get
        {
            var data = new TheoryData<string, bool>();

            foreach (var (name, requiresApproval, _, _) in _tools)
            {
                data.Add(name, requiresApproval);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        var (_, _, startup, category) = _tools.Single(tool => tool.Name == toolName);
        var tools = GetTools(startup);

        var found = tools.TryGetValue(toolName, out var tool);

        Assert.True(found, $"The tool '{toolName}' is not registered by {startup.FullName}.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal(category, tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void EachStartup_RegistersOnlyItsToolsAndBelongsToItsFeature()
    {
        foreach (var (startup, (featureId, required)) in _features)
        {
            var tools = GetTools(startup);
            var requiredFeatures = startup.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
                .Cast<RequireFeaturesAttribute>()
                .Single();
            var feature = startup.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
                .Cast<FeatureAttribute>()
                .Single();

            Assert.Equal(_tools.Count(tool => tool.Startup == startup), tools.Count);
            Assert.Equal(required.Order(StringComparer.Ordinal), requiredFeatures.RequiredFeatureNames.Order(StringComparer.Ordinal));
            Assert.Equal(featureId, feature.FeatureName);
        }
    }

    [Fact]
    public void Startups_RegisterFourteenToolsWithFiveApprovals()
    {
        var tools = _features.Keys.SelectMany(startup => GetTools(startup).Values).ToList();

        Assert.Equal(14, tools.Count);
        Assert.Equal(14, tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, tools.Count(tool => tool.RequiresApproval));
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

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
