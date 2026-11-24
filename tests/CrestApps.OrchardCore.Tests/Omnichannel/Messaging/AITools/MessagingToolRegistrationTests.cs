using System.Globalization;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Omnichannel.Messaging;
using CrestApps.OrchardCore.Omnichannel.Messaging.AI;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.AITools;

/// <summary>
/// The messaging workspace tools are registered by the workspace feature with a title, a description and a category,
/// ask for approval where the catalog says so, and refuse a caller who is not signed in or lacks the permission.
/// </summary>
public sealed class MessagingToolRegistrationTests
{
    private static readonly (string Name, bool RequiresApproval)[] _tools =
    [
        (ListMessagingConversationsTool.TheName, false),
        (GetMessagingConversationTool.TheName, false),
        (SendMessagingMessageTool.TheName, true),
        (UpdateMessagingConversationTool.TheName, false),
        (TransferMessagingConversationTool.TheName, false),
        (SetMessagingAvailabilityTool.TheName, false),
        (ListMessagingBroadcastsTool.TheName, false),
        (SendMessagingBroadcastTool.TheName, true),
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
    public void Startup_RegistersOnlyTheseToolsAndBelongsToTheWorkspaceFeature()
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
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(MessagingConstants.Feature.Workspace, feature.FeatureName);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var harness = new MessagingToolHarness();
        harness.AddConversation();
        var services = harness.Build(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        // Assert
        Assert.Equal(MessagingAIToolBase.NotAllowedMessage, result);
        AssertNothingChanged(harness);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName)
    {
        // Arrange
        var harness = new MessagingToolHarness();
        harness.AddConversation();
        var services = harness.Build(authorize: (_, _) => false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, Arguments());

        // Assert
        Assert.Equal(MessagingAIToolBase.NotAllowedMessage, result);
        AssertNothingChanged(harness);
    }

    private static void AssertNothingChanged(MessagingToolHarness harness)
    {
        Assert.Empty(harness.ConversationService.Invocations);
        Assert.Empty(harness.Availability.Invocations);
        Assert.Empty(harness.Templates.Created);
        Assert.Empty(harness.Templates.Deleted);
        Assert.Empty(harness.Broadcasts.Created);
    }

    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["conversationId"] = "conversation-1",
            ["body"] = "Hello",
            ["action"] = "close",
            ["targetType"] = "queue",
            ["targetId"] = "billing",
            ["available"] = true,
            ["id"] = "template-1",
            ["name"] = "Something",
            ["fromAddressId"] = "line-1",
            ["contactIds"] = new[] { "contact-1" },
        };

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListMessagingConversationsTool.TheName => new ListMessagingConversationsTool(),
            GetMessagingConversationTool.TheName => new GetMessagingConversationTool(),
            SendMessagingMessageTool.TheName => new SendMessagingMessageTool(),
            UpdateMessagingConversationTool.TheName => new UpdateMessagingConversationTool(),
            TransferMessagingConversationTool.TheName => new TransferMessagingConversationTool(),
            SetMessagingAvailabilityTool.TheName => new SetMessagingAvailabilityTool(),
            ListMessagingBroadcastsTool.TheName => new ListMessagingBroadcastsTool(),
            _ => new SendMessagingBroadcastTool(),
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
