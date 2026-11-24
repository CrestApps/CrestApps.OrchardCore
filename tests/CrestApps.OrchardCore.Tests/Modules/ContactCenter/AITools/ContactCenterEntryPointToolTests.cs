using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Agent;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The entry point tools go through the real entry point manager and handler, apply the editors' rules, validate the
/// phone menu, never route to an AI agent and never touch what other features keep on an entry point.
/// </summary>
public sealed class ContactCenterEntryPointToolTests
{
    private readonly ContactCenterSetupToolHarness _harness = new();

    public ContactCenterEntryPointToolTests()
    {
        _harness.AddQueue("billing", "Billing");
        _harness.AddQueue("sales", "Sales");
        _harness.AddAgent("agent-1", "omar");
        _harness.Addresses.Entries.Add(new OmnichannelChannelEndpoint { ItemId = "line-1", DisplayText = "Support line", Value = "+15550109000", Capabilities = ["Phone"] });
    }

    [Fact]
    public async Task SaveEntryPoint_CreatesAPhoneEntryPointThatRoutesToAQueue()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveContactCenterEntryPointTool(), _harness.Build(), new()
        {
            ["name"] = "Sales line",
            ["channel"] = "Phone",
            ["addressIds"] = new[] { "line-1" },
            ["targetType"] = "Queue",
            ["targetQueueId"] = "sales",
            ["targetAgentId"] = "agent-1",
            ["closedAction"] = "Voicemail",
            ["ringTimeoutSeconds"] = 30,
        }));

        // Assert
        Assert.Equal("created", json.GetProperty("saved").GetString());
        var entryPoint = Assert.Single(_harness.EntryPoints.Entries);
        Assert.Equal("Phone", entryPoint.Channel);
        Assert.Equal("sales", entryPoint.TargetQueueId);
        Assert.Null(entryPoint.TargetAgentId);
        Assert.Equal(EntryPointClosedAction.Voicemail, entryPoint.ClosedAction);
        Assert.Equal(["line-1"], entryPoint.AddressIds);
    }

    [Fact]
    public async Task SaveEntryPoint_RefusesRoutingToAnAIAgent()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterEntryPointTool(), _harness.Build(), new()
        {
            ["name"] = "AI line",
            ["channel"] = "Phone",
            ["targetType"] = "AIAgent",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: routing to an AI agent is set on the AI agent's card", result);
        Assert.Empty(_harness.EntryPoints.Entries);
    }

    [Fact]
    public async Task SaveEntryPoint_RefusesAPhoneMenuTheHandlerRefuses()
    {
        // Arrange
        AddEntryPoint();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterEntryPointTool(), _harness.Build(), new()
        {
            ["id"] = "entry-1",
            ["phoneMenu"] = JsonNode.Parse("""
                {
                  "rootNodeId": "main",
                  "nodes": [
                    { "nodeId": "main", "prompt": "Press 1 for sales.", "options": [
                      { "digit": "1", "action": { "kind": "RouteToQueue", "targetId": "sales" } },
                      { "digit": "1", "action": { "kind": "RouteToQueue", "targetId": "billing" } }
                    ] }
                  ]
                }
                """),
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", result);
        Assert.Contains("answers key '1' more than once", result);
        Assert.Null(_harness.EntryPoints.Entries.Single().IvrFlow);
    }

    [Fact]
    public async Task SaveEntryPoint_AddsAMenuOptionAndKeepsOtherFeaturesSettings()
    {
        // Arrange
        var entryPoint = AddEntryPoint();
        entryPoint.Properties["MessagingEntryPointSettings"] = new JsonObject { ["Enabled"] = true };

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveContactCenterEntryPointTool(), _harness.Build(), new()
        {
            ["id"] = "entry-1",
            ["phoneMenu"] = JsonNode.Parse("""
                {
                  "rootNodeId": "main",
                  "nodes": [
                    { "nodeId": "main", "prompt": "Press 1 for sales, 3 for billing.", "options": [
                      { "digit": "1", "action": { "kind": "RouteToQueue", "targetId": "sales" } },
                      { "digit": "3", "action": { "kind": "RouteToQueue", "targetId": "billing" } }
                    ] }
                  ]
                }
                """),
        }));

        // Assert
        Assert.Equal("updated", json.GetProperty("saved").GetString());
        var saved = _harness.EntryPoints.Entries.Single();
        Assert.Equal(["1", "3"], saved.IvrFlow.Nodes.Single().Options.Select(option => option.Digit));
        Assert.Equal(IvrActionKind.RouteToQueue, saved.IvrFlow.Nodes.Single().Options[1].Action.Kind);
        Assert.Equal("sales", saved.TargetQueueId);
        Assert.True(saved.Properties.ContainsKey("MessagingEntryPointSettings"));
        Assert.Contains("MessagingEntryPointSettings", json.GetProperty("entryPoint").GetProperty("otherSettings").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task SaveEntryPoint_RefusesAnExternalTransferThatIsNotAnApprovedDestination()
    {
        // Arrange
        AddEntryPoint();
        _harness.ExternalDestinations.Add(new ContactCenterExternalDestination { Id = "front-desk", E164Address = "+15550100000", Enabled = true });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterEntryPointTool(), _harness.Build(), new()
        {
            ["id"] = "entry-1",
            ["phoneMenu"] = JsonNode.Parse("""
                {
                  "rootNodeId": "main",
                  "nodes": [
                    { "nodeId": "main", "prompt": "Press 9.", "options": [
                      { "digit": "9", "action": { "kind": "ExternalTransfer", "targetId": "+15559999999" } }
                    ] }
                  ]
                }
                """),
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", result);
        Assert.Contains("is not an enabled external destination", result);
    }

    [Fact]
    public async Task SaveEntryPoint_RefusesCallSettingsWithoutInboundVoice()
    {
        // Arrange
        _harness.Features.Remove(ContactCenterConstants.Feature.InboundVoice);
        AddEntryPoint();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterEntryPointTool(), _harness.Build(), new()
        {
            ["id"] = "entry-1",
            ["closedAction"] = "Reject",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: closedAction is a call setting", result);
    }

    private ContactCenterEntryPoint AddEntryPoint()
    {
        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "entry-1",
            Name = "Main line",
            Channel = "Phone",
            AddressIds = ["line-1"],
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = "sales",
        };

        _harness.EntryPoints.Entries.Add(entryPoint);

        return entryPoint;
    }
}
