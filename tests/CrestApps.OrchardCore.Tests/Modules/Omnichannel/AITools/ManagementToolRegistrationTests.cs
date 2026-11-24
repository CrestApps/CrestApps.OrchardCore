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
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The subject flow, not-in-service, AI call and text, consent, address and CRM tools are registered by the feature that
/// owns their records with a title, a description and a category, ask for approval where the catalog says so, and refuse
/// a caller who is not signed in or lacks the permission. Addresses are read, and lead statuses and opportunity stages
/// read and written, with the generic record tools (see <see cref="OmnichannelRecordKindTests"/>).
/// </summary>
public sealed class ManagementToolRegistrationTests
{
    private static readonly (string Name, bool RequiresApproval, Type Startup)[] _tools =
    [
        (ListSubjectFlowsTool.TheName, false, typeof(ManagementAIToolsStartup)),
        (SaveSubjectActionTool.TheName, false, typeof(ManagementAIToolsStartup)),
        (ListNotInServiceNumbersTool.TheName, false, typeof(ManagementAIToolsStartup)),
        (SetNumberNotInServiceTool.TheName, false, typeof(ManagementAIToolsStartup)),
        (StartAIActivityTool.TheName, true, typeof(ManagementAIToolsStartup)),
        (RecordContactOptOutTool.TheName, false, typeof(ManagementAIToolsStartup)),
        (CheckContactConsentTool.TheName, false, typeof(ManagementAIToolsStartup)),
        (CancelAIActivityTool.TheName, false, typeof(ManagementAIToolsStartup)),
        (SaveOmnichannelAddressTool.TheName, false, typeof(ChannelEndpointsAIToolsStartup)),
        (ConvertLeadTool.TheName, false, typeof(CrmAIToolsStartup)),
    ];

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

    [Theory]
    [InlineData(typeof(ManagementAIToolsStartup), OmnichannelConstants.Features.Managements)]
    [InlineData(typeof(ChannelEndpointsAIToolsStartup), OmnichannelConstants.Features.ChannelEndpoints)]
    [InlineData(typeof(CrmAIToolsStartup), OmnichannelConstants.Features.Crm)]
    public void Startup_RegistersItsToolsWithTheAIFeatureAndBelongsToItsFeature(Type startup, string featureId)
    {
        // Act
        var registered = GetTools(startup);
        var required = startup.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();
        var feature = startup.GetCustomAttributes(typeof(FeatureAttribute), inherit: false)
            .Cast<FeatureAttribute>()
            .Single();

        // Assert
        Assert.Equal(_tools.Where(tool => tool.Startup == startup).Select(tool => tool.Name).Order(), registered.Keys.Order());
        Assert.Contains(AIConstants.Feature.Area, required.RequiredFeatureNames);
        Assert.Equal(featureId, feature.FeatureName);
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
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
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
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        AssertNothingChanged(harness);
    }

    private static ManagementToolHarness CreateHarness()
    {
        var harness = new ManagementToolHarness();
        harness.AddSubjectType();
        harness.Dispositions.Add(new CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelDisposition { ItemId = "disposition-1", Name = "No answer" });
        harness.Activities["activity-1"] = new CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelActivity
        {
            ItemId = "activity-1",
            AIProfileId = "profile-1",
            Status = CrestApps.OrchardCore.Omnichannel.Core.Models.ActivityStatus.Scheduled,
        };

        var lead = new ContentItem { ContentItemId = "lead-1", ContentType = "Lead" };
        lead.Apply(nameof(CrestApps.OrchardCore.Omnichannel.Core.Models.LeadPart), new CrestApps.OrchardCore.Omnichannel.Core.Models.LeadPart());
        harness.ContentItems["lead-1"] = lead;

        return harness;
    }

    private static void AssertNothingChanged(ManagementToolHarness harness)
    {
        Assert.Empty(harness.CreatedActions);
        Assert.Empty(harness.CreatedActivities);
        Assert.Empty(harness.UpdatedActivities);
        Assert.Empty(harness.Endpoints.Created);
        Assert.Empty(harness.LeadStatuses.Created);
        Assert.Empty(harness.OpportunityStages.Created);
        Assert.Empty(harness.RecordedConsent);
    }

    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["id"] = "activity-1",
            ["subjectContentType"] = "SalesCall",
            ["actionType"] = "Finish",
            ["dispositionId"] = "disposition-1",
            ["phoneNumbers"] = new[] { "+17025550123" },
            ["notInService"] = true,
            ["channel"] = "call",
            ["to"] = "+15550102030",
            ["address"] = "+15550102030",
            ["aiProfileId"] = "profile-1",
            ["addressType"] = "PhoneNumber",
            ["displayText"] = "Support line",
            ["value"] = "+15550109000",
            ["leadId"] = "lead-1",
            ["name"] = "Something",
        };

    internal static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListSubjectFlowsTool.TheName => new ListSubjectFlowsTool(),
            SaveSubjectActionTool.TheName => new SaveSubjectActionTool(),
            ListNotInServiceNumbersTool.TheName => new ListNotInServiceNumbersTool(),
            SetNumberNotInServiceTool.TheName => new SetNumberNotInServiceTool(),
            StartAIActivityTool.TheName => new StartAIActivityTool(),
            RecordContactOptOutTool.TheName => new RecordContactOptOutTool(),
            CancelAIActivityTool.TheName => new CancelAIActivityTool(),
            SaveOmnichannelAddressTool.TheName => new SaveOmnichannelAddressTool(),
            CheckContactConsentTool.TheName => new CheckContactConsentTool(),
            _ => new ConvertLeadTool(),
        };

    private static Dictionary<string, AIToolDefinitionEntry> GetTools()
    {
        var all = new Dictionary<string, AIToolDefinitionEntry>(StringComparer.Ordinal);

        foreach (var startup in new[] { typeof(ManagementAIToolsStartup), typeof(ChannelEndpointsAIToolsStartup), typeof(CrmAIToolsStartup) })
        {
            foreach (var (name, entry) in GetTools(startup))
            {
                all[name] = entry;
            }
        }

        return all;
    }

    private static Dictionary<string, AIToolDefinitionEntry> GetTools(Type startupType)
    {
        var services = new ServiceCollection();
        var localizerType = typeof(PassThroughLocalizer<>).MakeGenericType(startupType);
        var startup = (StartupBase)Activator.CreateInstance(startupType, Activator.CreateInstance(localizerType));

        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
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
