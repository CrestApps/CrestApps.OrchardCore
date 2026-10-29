using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An AI voice agent answering an inbound call has the custom SIP headers the call arrived with as conversation
/// variables, set before the AI Conversation Starting workflows run so they can route on them.
/// </summary>
public sealed partial class InboundVoiceServiceTests
{
    [Fact]
    public async Task AnAIAgentsCall_CarriesTheCustomSipHeaders_AsVariables_BeforeTheStartingWorkflowsRun()
    {
        // Arrange
        var harness = AIAgentHarness(out var activity);
        Dictionary<string, string> seenByTheStartingWorkflows = null;
        harness.ConversationStarting
            .Setup(starting => starting.StartingAsync(activity, It.IsAny<CancellationToken>()))
            .Callback<OmnichannelActivity, CancellationToken>((created, _) => seenByTheStartingWorkflows = new Dictionary<string, string>(created.GetConversationVariables()))
            .ReturnsAsync(new AIConversationStartingResult());
        var service = harness.CreateService();
        var call = AIAgentCall();
        SipHeaderVariables.AddToMetadata(call.Metadata, [new("X-Account-Id", "42"), new("X-Menu-Choice", "billing")]);

        // Act
        var result = await service.HandleInboundAsync(call, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Routed);
        Assert.NotNull(seenByTheStartingWorkflows);
        Assert.Equal("42", seenByTheStartingWorkflows["sip_account_id"]);
        Assert.Equal("billing", seenByTheStartingWorkflows["sip_menu_choice"]);
        Assert.Equal("42", activity.GetConversationVariables()["sip_account_id"]);
    }

    [Fact]
    public async Task AnAIAgentsCall_WithoutCustomHeaders_GetsNoVariables()
    {
        // Arrange
        var harness = AIAgentHarness(out var activity);
        var service = harness.CreateService();

        // Act
        await service.HandleInboundAsync(AIAgentCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(activity.GetConversationVariables());
    }

    private static Harness AIAgentHarness(out OmnichannelActivity activity)
    {
        var harness = new Harness();
        harness.SetupNoContext();
        var created = new OmnichannelActivity { ItemId = "act1" };
        var answerer = new Mock<IInboundAIVoiceAnswerer>();
        answerer.SetupGet(candidate => candidate.ProviderName).Returns("TestProvider");
        harness.AIVoiceAnswerers.Add(answerer.Object);
        harness.ActivityManager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        harness.EntryPointResolver
            .Setup(resolver => resolver.ResolveAsync("+15553334444", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntryPointRoutingPlan
            {
                EntryPoint = new ContactCenterEntryPoint { ItemId = "front-desk" },
                IsOpen = true,
                RouteToAIAgent = true,
                TargetAIProfileId = "front-desk-profile",
            });

        activity = created;

        return harness;
    }

    private static InboundVoiceEvent AIAgentCall()
        => new()
        {
            ProviderName = "TestProvider",
            ProviderCallId = "call-1",
            FromAddress = "+15551112222",
            ToAddress = "+15553334444",
        };
}
