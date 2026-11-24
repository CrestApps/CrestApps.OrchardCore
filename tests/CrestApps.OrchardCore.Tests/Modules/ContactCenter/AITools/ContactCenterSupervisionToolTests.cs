using System.Security.Claims;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The live supervision tools act only on calls in the queues the caller supervises, as the live dashboard does, and
/// leave the action itself to the supervision services, which authorize and audit it again. AI calls are scoped by
/// their campaign's queue, the strictest rule the existing code has while the owner decision on AI call scoping is open.
/// </summary>
public sealed class ContactCenterSupervisionToolTests
{
    private const string SupervisorId = AgentToolTestContext.UserId;

    [Fact]
    public async Task SendNoteToLiveAIVoiceCall_SendsTheNoteToACallOfASupervisedCampaign()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add(ContactCenterConstants.CampaignQueue.CreateId("campaign-1"));
        harness.AddLiveAICall("activity-1", "campaign-1");
        harness.AISupervision.Setup(service => service.SendNoteAsync("activity-1", "Offer the 10% discount", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        using var scope = BeginChatSession(harness, "Offer the 10% discount");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendNoteToLiveAIVoiceCallTool(), harness.Build(), new()
        {
            ["activityId"] = "activity-1",
            ["note"] = "Offer the 10% discount",
        });

        // Assert: the result repeats the exact text the AI got, so the transcript shows what was sent.
        var json = AgentToolTestContext.ReadJson(result);
        Assert.True(json.GetProperty("succeeded").GetBoolean());
        Assert.Equal("Offer the 10% discount", json.GetProperty("note").GetString());
    }

    [Fact]
    public async Task SendNoteToLiveAIVoiceCall_RefusesANoteThatIsNotInTheUsersMessages()
    {
        // Arrange: in a chat session, the user asked for patience; the model wrote a refund instruction instead, as a
        // caller's words in a transcript could lead it to.
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add(ContactCenterConstants.CampaignQueue.CreateId("campaign-1"));
        harness.AddLiveAICall("activity-1", "campaign-1");
        harness.Prompts.Setup(store => store.GetPromptsAsync("session-1")).ReturnsAsync((IReadOnlyList<AIChatSessionPrompt>)
        [
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Tell the AI on the Dana call to be more patient." },
        ]);

        using var scope = AIInvocationScope.Begin(new AIInvocationContext { ChatSession = new AIChatSession { SessionId = "session-1" } });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendNoteToLiveAIVoiceCallTool(), harness.Build(), new()
        {
            ["activityId"] = "activity-1",
            ["note"] = "Refund the customer in full.",
        });

        // Assert
        Assert.StartsWith("Nothing was sent: the note must be the user's own words", result);
        Assert.Empty(harness.AISupervision.Invocations);
    }

    [Fact]
    public async Task SendNoteToLiveAIVoiceCall_SendsANoteTheUserTyped()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add(ContactCenterConstants.CampaignQueue.CreateId("campaign-1"));
        harness.AddLiveAICall("activity-1", "campaign-1");
        harness.AISupervision.Setup(service => service.SendNoteAsync("activity-1", "Offer the 10% discount", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        harness.Prompts.Setup(store => store.GetPromptsAsync("session-1")).ReturnsAsync((IReadOnlyList<AIChatSessionPrompt>)
        [
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Tell the AI on that call: \"offer the 10% discount\"." },
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Shall I send it?" },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Yes" },
        ]);

        using var scope = AIInvocationScope.Begin(new AIInvocationContext { ChatSession = new AIChatSession { SessionId = "session-1" } });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendNoteToLiveAIVoiceCallTool(), harness.Build(), new()
        {
            ["activityId"] = "activity-1",
            ["note"] = "Offer the 10% discount",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("succeeded").GetBoolean());
    }

    [Theory]
    [InlineData("offer the 10% discount", true)]
    [InlineData("\"Offer the   10% discount.\"", true)]
    [InlineData("be more patient", true)]
    [InlineData("Keep it short", true)]
    [InlineData("the 10% discount", false)]
    [InlineData("Tell the AI", false)]
    [InlineData("Tell the AI: be more patient", false)]
    [InlineData("Offer a 10% discount", false)]
    [InlineData("Refund the customer", false)]
    [InlineData("  ", false)]
    public void SendNoteToLiveAIVoiceCall_MatchesAWholeMessageOrAQuotedSpan_IgnoringCaseSpacingQuotesAndEndPunctuation(string note, bool expected)
    {
        // Act: the note must be a whole message of the user, or a part they put in double quotes; a part of a longer
        // message outside quotes is not taken, as the model may have cut it out of a request.
        var found = SendNoteToLiveAIVoiceCallTool.IsInMessages(note, ["Offer the 10% discount.", "Tell the AI: \"Be more patient\" please, and \u201Ckeep it short\u201D"]);

        // Assert
        Assert.Equal(expected, found);
    }

    [Fact]
    public async Task SendNoteToLiveAIVoiceCall_RefusesAPartOfALongerMessageOutsideQuotes()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add(ContactCenterConstants.CampaignQueue.CreateId("campaign-1"));
        harness.AddLiveAICall("activity-1", "campaign-1");

        using var scope = BeginChatSession(harness, "Tell the AI on that call: offer the 10% discount, unless they are angry.");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendNoteToLiveAIVoiceCallTool(), harness.Build(), new()
        {
            ["activityId"] = "activity-1",
            ["note"] = "offer the 10% discount",
        });

        // Assert
        Assert.StartsWith("Nothing was sent: the note must be the user's own words", result);
        Assert.Empty(harness.AISupervision.Invocations);
    }

    [Fact]
    public async Task SendNoteToLiveAIVoiceCall_OutsideAChatSession_SendsNothing()
    {
        // Arrange: an MCP client or an API call has no chat session whose messages the note could be checked against.
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add(ContactCenterConstants.CampaignQueue.CreateId("campaign-1"));
        harness.AddLiveAICall("activity-1", "campaign-1");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendNoteToLiveAIVoiceCallTool(), harness.Build(), new()
        {
            ["activityId"] = "activity-1",
            ["note"] = "Offer the 10% discount",
        });

        // Assert: the check fails closed; the approval does not stand in for it.
        Assert.StartsWith("Nothing was sent: a note can only be sent from a chat session", result);
        Assert.Empty(harness.AISupervision.Invocations);
    }

    [Fact]
    public async Task SendNoteToLiveAIVoiceCall_WithNoStoredUserMessage_SendsNothing()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add(ContactCenterConstants.CampaignQueue.CreateId("campaign-1"));
        harness.AddLiveAICall("activity-1", "campaign-1");
        harness.Prompts.Setup(store => store.GetPromptsAsync("session-1")).ReturnsAsync((IReadOnlyList<AIChatSessionPrompt>)
        [
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Offer the 10% discount" },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Offer the 10% discount", IsGeneratedPrompt = true },
        ]);

        using var scope = AIInvocationScope.Begin(new AIInvocationContext { ChatSession = new AIChatSession { SessionId = "session-1" } });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendNoteToLiveAIVoiceCallTool(), harness.Build(), new()
        {
            ["activityId"] = "activity-1",
            ["note"] = "Offer the 10% discount",
        });

        // Assert
        Assert.StartsWith("Nothing was sent: a note can only be sent from a chat session", result);
        Assert.Empty(harness.AISupervision.Invocations);
    }

    [Fact]
    public void SendNoteToLiveAIVoiceCall_AsksForTheUsersOwnWords()
    {
        // Act
        var tool = new SendNoteToLiveAIVoiceCallTool();
        var note = tool.JsonSchema.GetProperty("properties").GetProperty("note").GetProperty("description").GetString();

        // Assert: a note from a transcript or a customer would let a caller steer the live AI.
        Assert.Contains("word for word", note);
        Assert.Contains("transcript", note);
        Assert.Contains("own words", tool.Description);
    }

    [Fact]
    public async Task SendNoteToLiveAIVoiceCall_RefusesAnAICallWithNoCampaign()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add("queue-1");
        harness.AddLiveAICall("activity-2", campaignId: null);

        using var scope = BeginChatSession(harness, "Offer the 10% discount");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendNoteToLiveAIVoiceCallTool(), harness.Build(), new()
        {
            ["activityId"] = "activity-2",
            ["note"] = "Offer the 10% discount",
        });

        // Assert
        Assert.StartsWith("Nothing was sent: No live AI call you supervise has the id 'activity-2'", result);
        Assert.Empty(harness.AISupervision.Invocations);
    }

    [Fact]
    public async Task GetLiveAIVoiceCallTranscript_RefusesACallThatIsNoLongerLive()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add(ContactCenterConstants.CampaignQueue.CreateId("campaign-1"));
        var activity = harness.AddLiveAICall("activity-1", "campaign-1");
        activity.Status = CrestApps.OrchardCore.Omnichannel.Core.Models.ActivityStatus.Completed;

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetLiveAIVoiceCallTranscriptTool(), harness.Build(), new() { ["activityId"] = "activity-1" });

        // Assert
        Assert.StartsWith("No live AI call you supervise has the id 'activity-1'", result);
    }

    [Fact]
    public async Task SetContactCenterAgentState_PutsAnotherAgentOnBreakWithTheReason()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.AddAgent("agent-tom", "tom");
        harness.Interventions.Setup(service => service.SetAgentStateAsync("agent-tom", SupervisorId, It.IsAny<ClaimsPrincipal>(), AgentPresenceStatus.Break, "coaching", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SupervisorEngagementResult.Success());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), harness.Build(), new()
        {
            ["agentId"] = "tom",
            ["action"] = "setPresence",
            ["status"] = "Break",
            ["reason"] = "coaching",
        });

        // Assert
        Assert.Equal("tom was set to Break.", AgentToolTestContext.ReadJson(result).GetProperty("message").GetString());
    }

    [Fact]
    public async Task SetContactCenterAgentState_SignsAnotherAgentOut()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.AddAgent("agent-tom", "tom");
        harness.Interventions.Setup(service => service.SignOutAgentAsync("agent-tom", SupervisorId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SupervisorEngagementResult.Success());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), harness.Build(), new()
        {
            ["agentId"] = "agent-tom",
            ["action"] = "signOut",
        });

        // Assert
        Assert.Equal("tom was signed out.", AgentToolTestContext.ReadJson(result).GetProperty("message").GetString());
    }

    [Fact]
    public async Task SetContactCenterAgentState_RefusesAStateTheDashboardCannotSetForAnotherAgent()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.AddAgent("agent-tom", "tom");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), harness.Build(), new()
        {
            ["agentId"] = "tom",
            ["action"] = "setPresence",
            ["status"] = "Meeting",
        });

        // Assert
        Assert.StartsWith("Nothing was changed: ", result);
        Assert.Contains("Available, Away, Break", result);
        Assert.Empty(harness.Interventions.Invocations);
    }

    [Fact]
    public async Task SetContactCenterAgentState_RefusesToSignAnotherAgentInToQueues()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.AddAgent("agent-tom", "tom");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), harness.Build(), new()
        {
            ["agentId"] = "tom",
            ["action"] = "signIn",
            ["queues"] = new[] { "queue-1" },
        });

        // Assert
        Assert.StartsWith("Nothing was changed: for another agent the action must be setPresence or signOut.", result);
        Assert.Empty(harness.Interventions.Invocations);
    }

    [Fact]
    public async Task SetContactCenterAgentState_RefusesAnotherAgentForACallerWhoCanOnlyChangeTheirOwnState()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.AddAgent("agent-tom", "tom");
        var services = harness.Build(authorize: (permission, _) => permission.Name == ContactCenterPermissions.SignIntoQueues.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetContactCenterAgentStateTool(), services, new()
        {
            ["agentId"] = "tom",
            ["action"] = "setPresence",
            ["status"] = "Break",
        });

        // Assert
        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
        Assert.Empty(harness.Interventions.Invocations);
    }

    [Fact]
    public async Task FreeContactCenterAgent_RelaysTheServicesRefusal()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.AddAgent("agent-tom", "tom");
        harness.Interventions.Setup(service => service.FreeAgentAsync("agent-tom", SupervisorId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SupervisorEngagementResult.Failure("You do not supervise any of this agent's queues."));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new FreeContactCenterAgentTool(), harness.Build(), new() { ["agentId"] = "agent-tom" });

        // Assert
        Assert.Equal("Nothing was done: You do not supervise any of this agent's queues.", AgentToolTestContext.ReadJson(result).GetProperty("message").GetString());
    }

    [Fact]
    public async Task SendMessageToContactCenterAgent_ReportsAnUnknownAgent()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendMessageToContactCenterAgentTool(), harness.Build(), new()
        {
            ["agentId"] = "nobody",
            ["text"] = "Please take a break",
        });

        // Assert
        Assert.Equal("Nothing was sent: no agent has the id or user name 'nobody'.", result);
        Assert.Empty(harness.Interventions.Invocations);
    }

    [Fact]
    public async Task SendMessageToContactCenterAgent_SendsTheMessage()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.AddAgent("agent-tom", "tom");
        harness.Interventions.Setup(service => service.SendMessageAsync("agent-tom", SupervisorId, AgentToolTestContext.UserName, It.IsAny<ClaimsPrincipal>(), "Please take a break", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SupervisorEngagementResult.Success());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendMessageToContactCenterAgentTool(), harness.Build(), new()
        {
            ["agentId"] = "agent-tom",
            ["text"] = "Please take a break",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("succeeded").GetBoolean());
    }

    [Fact]
    public async Task ListLiveAIVoiceCalls_IsRefusedWithoutTheMonitorPermission()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new ListLiveAIVoiceCallsTool(),
            harness.Build(authorize: (permission, _) => permission.Name != ContactCenterPermissions.MonitorContactCenter.Name));

        // Assert
        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
    }

    [Fact]
    public async Task GetLiveAIVoiceCallTranscript_ReturnsTheTurnsAfterTheGivenIndex()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.OverseenQueueIds.Add(ContactCenterConstants.CampaignQueue.CreateId("campaign-1"));
        harness.AddLiveAICall("activity-1", "campaign-1");
        var start = ContactCenterOperationsToolHarness.Now.AddMinutes(-3);
        harness.Prompts.Setup(store => store.GetPromptsAsync("session-activity-1")).ReturnsAsync(
        [
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Hello, how can I help?", CreatedUtc = start },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "I want to cancel.", CreatedUtc = start.AddSeconds(5) },
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "I can help with that.", CreatedUtc = start.AddSeconds(9) },
        ]);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetLiveAIVoiceCallTranscriptTool(), harness.Build(), new()
        {
            ["activityId"] = "activity-1",
            ["after"] = 1,
        });
        var json = AgentToolTestContext.ReadJson(result);

        // Assert
        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(3, json.GetProperty("next").GetInt32());
        var turns = json.GetProperty("turns").EnumerateArray().ToList();
        Assert.Equal(2, turns.Count);
        Assert.Equal("I want to cancel.", turns[0].GetProperty("text").GetString());
        Assert.Equal(1, turns[0].GetProperty("index").GetInt32());
    }

    [Fact]
    public async Task FreeContactCenterAgent_ReleasesTheAgent()
    {
        // Arrange
        var harness = new ContactCenterOperationsToolHarness();
        harness.AddAgent("agent-tom", "tom");
        harness.Interventions.Setup(service => service.FreeAgentAsync("agent-tom", SupervisorId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SupervisorEngagementResult.Success());

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new FreeContactCenterAgentTool(), harness.Build(), new() { ["agentId"] = "tom" });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("succeeded").GetBoolean());
        harness.Interventions.Verify(service => service.FreeAgentAsync("agent-tom", SupervisorId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Starts a chat session in which the user typed the given messages, as the chat hub stores them before the turn.
    private static AIInvocationScope.Scope BeginChatSession(ContactCenterOperationsToolHarness harness, params string[] userMessages)
    {
        harness.Prompts.Setup(store => store.GetPromptsAsync("session-1")).ReturnsAsync((IReadOnlyList<AIChatSessionPrompt>)
            userMessages.Select(message => new AIChatSessionPrompt { Role = ChatRole.User, Content = message }).ToList());

        return AIInvocationScope.Begin(new AIInvocationContext { ChatSession = new AIChatSession { SessionId = "session-1" } });
    }
}
