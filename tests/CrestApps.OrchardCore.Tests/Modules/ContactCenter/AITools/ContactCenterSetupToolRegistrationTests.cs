using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The Contact Center setup tools are registered by the feature that owns their records, with a title, a description and
/// a category, ask for approval where the catalog says so, and refuse a caller who is not signed in or lacks the
/// permission before anything is read or changed. Queue groups, reason codes and dialer profiles, and the reads of
/// queues, calendars and entry points, are record kinds of the generic record tools instead (see
/// <see cref="ContactCenterRecordKindTests"/>).
/// </summary>
public sealed class ContactCenterSetupToolRegistrationTests
{
    private static readonly (string Name, bool RequiresApproval, Type Startup)[] _tools =
    [
        (SaveContactCenterQueueTool.TheName, false, typeof(QueuesAIToolsStartup)),
        (DeleteContactCenterRecordTool.TheName, true, typeof(QueuesAIToolsStartup)),
        (SaveContactCenterEntryPointTool.TheName, false, typeof(EntryPointsAIToolsStartup)),
        (SaveBusinessHoursCalendarTool.TheName, false, typeof(BusinessHoursAIToolsStartup)),
        (CheckBusinessHoursTool.TheName, false, typeof(BusinessHoursAIToolsStartup)),
        (ListContactCenterAgentsTool.TheName, false, typeof(AgentsAIToolsStartup)),
        (SaveContactCenterAgentTool.TheName, true, typeof(AgentEntitlementsAIToolsStartup)),
        (ListCallbackRequestsTool.TheName, false, typeof(DialerAIToolsStartup)),
    ];

    private static readonly Dictionary<Type, string> _features = new()
    {
        [typeof(QueuesAIToolsStartup)] = ContactCenterConstants.Feature.Queues,
        [typeof(EntryPointsAIToolsStartup)] = ContactCenterConstants.Feature.EntryPoints,
        [typeof(BusinessHoursAIToolsStartup)] = ContactCenterConstants.Feature.BusinessHours,
        [typeof(AgentsAIToolsStartup)] = ContactCenterConstants.Feature.Agents,
        [typeof(AgentEntitlementsAIToolsStartup)] = ContactCenterConstants.Feature.AgentEntitlements,
        [typeof(DialerAIToolsStartup)] = ContactCenterConstants.Feature.Dialer,
    };

    public static TheoryData<string, bool> Tools
    {
        get
        {
            var data = new TheoryData<string, bool>();

            foreach (var (name, requiresApproval, _) in _tools)
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

            foreach (var (name, _, _) in _tools)
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
        var startup = _tools.Single(tool => tool.Name == toolName).Startup;
        var tools = GetTools(startup);

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered by {startup.Name}.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Contact Center", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Fact]
    public void EachStartup_RegistersOnlyItsToolsAndBelongsToItsFeature()
    {
        foreach (var (startup, featureId) in _features)
        {
            // Act
            var tools = GetTools(startup);
            var required = startup.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
                .Cast<RequireFeaturesAttribute>()
                .Single();
            var feature = startup.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
                .Cast<FeatureAttribute>()
                .Single();

            // Assert
            Assert.Equal(_tools.Count(tool => tool.Startup == startup), tools.Count);
            Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
            Assert.Equal(featureId, feature.FeatureName);
        }
    }

    [Fact]
    public void Startups_RegisterEightToolsWithTwoApprovals()
    {
        // Act
        var tools = _features.Keys.SelectMany(startup => GetTools(startup).Values).ToList();

        // Assert
        Assert.Equal(8, tools.Count);
        Assert.Equal(8, tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            new[] { DeleteContactCenterRecordTool.TheName, SaveContactCenterAgentTool.TheName },
            tools.Where(tool => tool.RequiresApproval).Select(tool => tool.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var harness = CreateHarness();
        var services = harness.Build(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        // Assert
        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
        AssertNothingChanged(harness);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName)
    {
        // Arrange
        var harness = CreateHarness();
        var services = harness.Build(authorize: (_, _) => false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        // Assert
        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
        AssertNothingChanged(harness);
    }

    private static ContactCenterSetupToolHarness CreateHarness()
    {
        var harness = new ContactCenterSetupToolHarness();
        harness.AddQueue("queue-1", "Billing");
        harness.AddCalendar("calendar-1", "Support");
        harness.AddAgent("agent-1", "maria");

        return harness;
    }

    private static void AssertNothingChanged(ContactCenterSetupToolHarness harness)
    {
        Assert.Single(harness.Queues.Entries);
        Assert.Equal(0, harness.Queues.UpdateCount);
        Assert.Equal("Billing", harness.Queues.Entries[0].Name);
        Assert.Single(harness.Calendars.Entries);
        Assert.Equal(0, harness.Calendars.UpdateCount);
        Assert.Empty(harness.QueueGroups.Entries);
        Assert.Empty(harness.EntryPoints.Entries);
        Assert.Empty(harness.ReasonCodes.Entries);
        Assert.Empty(harness.DialerProfiles.Entries);
        Assert.Empty(harness.CreatedAgents);
        Assert.Empty(harness.Presence.Invocations);
    }

    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["id"] = "queue-1",
            ["queue"] = "queue-1",
            ["entryPoint"] = "entry-1",
            ["calendar"] = "calendar-1",
            ["profile"] = "profile-1",
            ["kind"] = "queue",
            ["name"] = "Changed",
            ["agentId"] = "agent-1",
            ["maxConcurrentInteractions"] = 3,
        };

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            SaveContactCenterQueueTool.TheName => new SaveContactCenterQueueTool(),
            DeleteContactCenterRecordTool.TheName => new DeleteContactCenterRecordTool(),
            SaveContactCenterEntryPointTool.TheName => new SaveContactCenterEntryPointTool(),
            SaveBusinessHoursCalendarTool.TheName => new SaveBusinessHoursCalendarTool(),
            CheckBusinessHoursTool.TheName => new CheckBusinessHoursTool(),
            ListContactCenterAgentsTool.TheName => new ListContactCenterAgentsTool(),
            SaveContactCenterAgentTool.TheName => new SaveContactCenterAgentTool(),
            _ => new ListCallbackRequestsTool(),
        };

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
