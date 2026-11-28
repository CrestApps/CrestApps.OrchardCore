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
/// The Contact Center voice media, call recording, shared voicemail and live supervision tools are registered by the
/// feature of the page they mirror, with a title, a description and a category, ask for approval where the batch says
/// so, and refuse a caller who is not signed in or lacks the permission before anything is read or changed.
/// </summary>
public sealed class ContactCenterOperationsToolRegistrationTests
{
    private static readonly (string Name, bool RequiresApproval, Type Startup)[] _tools =
    [
        (ListVoiceMediaTool.TheName, false, typeof(VoiceMediaAIToolsStartup)),
        (SaveVoiceMediaTool.TheName, false, typeof(VoiceMediaAIToolsStartup)),
        (ResyncVoiceMediaTool.TheName, true, typeof(VoiceMediaAIToolsStartup)),
        (ListCallRecordingsTool.TheName, false, typeof(RecordingAIToolsStartup)),
        (GetCallRecordingTool.TheName, false, typeof(RecordingAIToolsStartup)),
        (ShareCallRecordingTool.TheName, true, typeof(RecordingAIToolsStartup)),
        (EraseCallRecordingTool.TheName, true, typeof(RecordingAIToolsStartup)),
        (ListSharedVoicemailsTool.TheName, false, typeof(SharedVoicemailAIToolsStartup)),
        (UpdateSharedVoicemailTool.TheName, false, typeof(SharedVoicemailAIToolsStartup)),
        (CallBackSharedVoicemailTool.TheName, true, typeof(SharedVoicemailAIToolsStartup)),
        (DeleteSharedVoicemailTool.TheName, true, typeof(SharedVoicemailAIToolsStartup)),
        (ListLiveAIVoiceCallsTool.TheName, false, typeof(SupervisionAIToolsStartup)),
        (GetLiveAIVoiceCallTranscriptTool.TheName, false, typeof(SupervisionAIToolsStartup)),
        (SendNoteToLiveAIVoiceCallTool.TheName, true, typeof(SupervisionAIToolsStartup)),
        (FreeContactCenterAgentTool.TheName, true, typeof(SupervisionAIToolsStartup)),
        (SendMessageToContactCenterAgentTool.TheName, false, typeof(SupervisionAIToolsStartup)),
    ];

    private static readonly Dictionary<Type, string> _features = new()
    {
        [typeof(VoiceMediaAIToolsStartup)] = ContactCenterConstants.Feature.Area,
        [typeof(RecordingAIToolsStartup)] = ContactCenterConstants.Feature.Recording,
        [typeof(SharedVoicemailAIToolsStartup)] = ContactCenterConstants.Feature.InboundVoice,
        [typeof(SupervisionAIToolsStartup)] = ContactCenterConstants.Feature.Supervision,
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
        Assert.Equal(toolName, CreateTool(toolName).Name);
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
    public void Startups_RegisterSixteenToolsWithSevenApprovals()
    {
        // Act
        var tools = _features.Keys.SelectMany(startup => GetTools(startup).Values).ToList();

        // Assert
        Assert.Equal(16, tools.Count);
        Assert.Equal(16, tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(7, tools.Count(tool => tool.RequiresApproval));
    }

    [Fact]
    public void Tools_DoNotTakeTheNamesOfTheSetupTools()
    {
        // Arrange
        var setupStartups = new[]
        {
            typeof(QueuesAIToolsStartup),
            typeof(EntryPointsAIToolsStartup),
            typeof(BusinessHoursAIToolsStartup),
            typeof(AgentsAIToolsStartup),
            typeof(AgentEntitlementsAIToolsStartup),
            typeof(DialerAIToolsStartup),
        };

        // Act
        var setupNames = setupStartups.SelectMany(startup => GetTools(startup).Keys).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Assert
        Assert.DoesNotContain(_tools, tool => setupNames.Contains(tool.Name));
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        var services = harness.Build(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        // Assert
        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
        AssertNothingReached(harness);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName)
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        var services = harness.Build(authorize: (_, _) => false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        // Assert
        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
        AssertNothingReached(harness);
    }

    internal static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListVoiceMediaTool.TheName => new ListVoiceMediaTool(),
            SaveVoiceMediaTool.TheName => new SaveVoiceMediaTool(),
            ResyncVoiceMediaTool.TheName => new ResyncVoiceMediaTool(),
            ListCallRecordingsTool.TheName => new ListCallRecordingsTool(),
            GetCallRecordingTool.TheName => new GetCallRecordingTool(),
            ShareCallRecordingTool.TheName => new ShareCallRecordingTool(),
            EraseCallRecordingTool.TheName => new EraseCallRecordingTool(),
            ListSharedVoicemailsTool.TheName => new ListSharedVoicemailsTool(),
            UpdateSharedVoicemailTool.TheName => new UpdateSharedVoicemailTool(),
            CallBackSharedVoicemailTool.TheName => new CallBackSharedVoicemailTool(),
            DeleteSharedVoicemailTool.TheName => new DeleteSharedVoicemailTool(),
            ListLiveAIVoiceCallsTool.TheName => new ListLiveAIVoiceCallsTool(),
            GetLiveAIVoiceCallTranscriptTool.TheName => new GetLiveAIVoiceCallTranscriptTool(),
            SendNoteToLiveAIVoiceCallTool.TheName => new SendNoteToLiveAIVoiceCallTool(),
            FreeContactCenterAgentTool.TheName => new FreeContactCenterAgentTool(),
            _ => new SendMessageToContactCenterAgentTool(),
        };

    // Arguments that would let each tool act, so a refusal is the permission check and not a missing argument.
    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["id"] = "item-1",
            ["name"] = "Changed",
            ["reason"] = "Privacy request",
            ["action"] = "end",
            ["note"] = "Offer the discount",
            ["text"] = "Please take a break",
            ["activityId"] = "activity-1",
            ["callType"] = "agentCall",
            ["callId"] = "interaction-1",
            ["agentId"] = "agent-1",
            ["state"] = "Break",
            ["targetType"] = "Queue",
            ["targetId"] = "queue-1",
        };

    private static void AssertNothingReached(ContactCenterOperationsToolHarness harness)
    {
        Assert.Empty(harness.VoiceMedia.Invocations);
        Assert.Empty(harness.Provisioning.Invocations);
        Assert.Empty(harness.Recordings.Invocations);
        Assert.Empty(harness.Governance.Invocations);
        Assert.Empty(harness.Voicemails.Invocations);
        Assert.Empty(harness.SoftPhones.Invocations);
        Assert.Empty(harness.Interventions.Invocations);
        Assert.Empty(harness.Monitoring.Invocations);
        Assert.Empty(harness.PhoneCalls.Invocations);
        Assert.Empty(harness.AISupervision.Invocations);
        Assert.Empty(harness.AITransfers.Invocations);
        Assert.Empty(harness.Activities.Invocations);
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
