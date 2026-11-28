using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.AI;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;
using Moq;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.AITools;

/// <summary>
/// The messaging workspace tools use the workspace's own services and conversation checks, never return what the
/// workspace keeps to itself, and refuse to reach a customer the workspace would not let the caller reach.
/// </summary>
public sealed class MessagingToolTests
{
    private readonly MessagingToolHarness _harness = new();

    [Fact]
    public async Task ListConversations_ShowsEveryConversationToASupervisorWithoutTheAISession()
    {
        // Arrange
        _harness.AddConversation();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListMessagingConversationsTool(), _harness.Build(), new()
        {
            ["scope"] = "all",
            ["holderAgentId"] = "agent-2",
        });

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());
        Assert.Equal("conversation-1", item.GetProperty("id").GetString());
        Assert.Equal("Where is my order?", item.GetProperty("lastMessagePreview").GetString());
        Assert.DoesNotContain("session-secret", result);
        Assert.DoesNotContain("5550107788", result);
        var query = Assert.Single(_harness.Queries);
        Assert.True(query.IncludeAll);
        Assert.Equal("agent-2", query.HolderAgentId);
    }

    [Fact]
    public async Task ListConversations_ShowsNothingToACallerWithoutAProfileWhoMayNotSeeAll()
    {
        // Arrange
        _harness.Agent = null;
        _harness.AddConversation();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new ListMessagingConversationsTool(),
            _harness.Build(authorize: (permission, _) => permission.Name != MessagingPermissions.ViewAllConversations.Name));

        // Assert
        Assert.Equal(0, AgentToolTestContext.ReadJson(result).GetProperty("total").GetInt32());
        Assert.Empty(_harness.Queries);
    }

    [Fact]
    public async Task GetConversation_SaysSoWhenTheCallerMayNotOpenIt()
    {
        // Arrange
        _harness.AddConversation();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new GetMessagingConversationTool(),
            _harness.Build(authorize: MessagingToolHarness.Deny(ConversationOperation.View)),
            new() { ["conversationId"] = "conversation-1" });

        // Assert
        Assert.StartsWith("No conversation you may open matches", result);
    }

    [Fact]
    public async Task SendMessage_RepliesInTheConversationAsTheCaller()
    {
        // Arrange
        _harness.AddConversation();
        MessagingSendRequest request = null;
        _harness.ConversationService.Setup(service => service.SendAsync(It.IsAny<MessagingSendRequest>(), It.IsAny<CancellationToken>()))
            .Callback((MessagingSendRequest value, CancellationToken _) => request = value)
            .ReturnsAsync(new MessagingSendResult { Succeeded = true, Message = new OmnichannelMessage { Id = "message-1", DeliveryStatus = "Sent" } });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendMessagingMessageTool(), _harness.Build(), new()
        {
            ["conversationId"] = "conversation-1",
            ["body"] = "Your order ships tomorrow.",
        }));

        // Assert
        Assert.True(json.GetProperty("sent").GetBoolean());
        Assert.Equal("conversation-1", request.ConversationId);
        Assert.Equal("Your order ships tomorrow.", request.Body);
        Assert.Equal("agent-1", request.ActingAgentId);
        Assert.Equal(AgentToolTestContext.UserId, request.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task SendMessage_RefusesAConversationTheCallerMayNotSendOn()
    {
        // Arrange
        _harness.AddConversation();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new SendMessagingMessageTool(),
            _harness.Build(authorize: MessagingToolHarness.Deny(ConversationOperation.Send)),
            new()
            {
                ["conversationId"] = "conversation-1",
                ["body"] = "Hello",
            });

        // Assert
        Assert.StartsWith("Nothing was sent: you are not allowed to send on this conversation", result);
        _harness.ConversationService.Verify(service => service.SendAsync(It.IsAny<MessagingSendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_RefusesOutsideBusinessHoursWithoutTheQuietHoursPermission()
    {
        // Arrange
        _harness.AddConversation(configure: conversation =>
        {
            conversation.OwnerType = ConversationOwnerType.Queue;
            conversation.OwnerId = "billing";
        });
        _harness.QueuePolicies.Setup(reader => reader.ReadAsync("billing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingQueuePolicy(true, 0, "office-hours"));
        _harness.BusinessHours.Setup(gate => gate.IsOpenAsync("office-hours", It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new SendMessagingMessageTool(),
            _harness.Build(authorize: (permission, _) => permission.Name != MessagingPermissions.SendDuringQuietHours.Name),
            new()
            {
                ["conversationId"] = "conversation-1",
                ["body"] = "Hello",
            });

        // Assert
        Assert.StartsWith("Nothing was sent:", result);
        Assert.Contains("outside business hours", result);
        _harness.ConversationService.Verify(service => service.SendAsync(It.IsAny<MessagingSendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_ListsTheAddressesWhenANewMessageHasNoSender()
    {
        // Arrange
        _harness.Endpoints.Add(new OmnichannelChannelEndpoint { ItemId = "line-1", DisplayText = "Support", Value = "+15550109000", Capabilities = ["SMS"] });
        _harness.Endpoints.Add(new OmnichannelChannelEndpoint { ItemId = "line-2", DisplayText = "Sales", Value = "+15550109001", Capabilities = ["SMS"] });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendMessagingMessageTool(), _harness.Build(), new()
        {
            ["to"] = "+15550107788",
            ["body"] = "Hello",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal(2, json.GetProperty("addresses").GetArrayLength());
        _harness.ConversationService.Verify(service => service.SendDirectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_StartsANewConversationFromTheChosenAddress()
    {
        // Arrange
        _harness.Endpoints.Add(new OmnichannelChannelEndpoint { ItemId = "line-1", DisplayText = "Support", Value = "+15550109000", Capabilities = ["SMS"] });
        _harness.ConversationService.Setup(service => service.SendDirectAsync("SMS", "+15550109000", "+12015550123", "Hello", "agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingSendResult { Succeeded = true, Message = new OmnichannelMessage { Id = "message-1", ConversationId = "conversation-9" } });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendMessagingMessageTool(), _harness.Build(), new()
        {
            ["to"] = "+12015550123",
            ["fromAddressId"] = "line-1",
            ["body"] = "Hello",
        }));

        // Assert
        Assert.True(json.GetProperty("sent").GetBoolean());
        Assert.Equal("conversation-9", json.GetProperty("conversationId").GetString());
    }

    [Fact]
    public async Task SendMessage_RefusesARecipientWhoseConversationTheCallerMayNotSendOn()
    {
        // Arrange
        _harness.AddConversation(configure: conversation => conversation.ContactAddress = "+12015550123");
        _harness.Endpoints.Add(new OmnichannelChannelEndpoint { ItemId = "line-1", DisplayText = "Support", Value = "+15550109000", Capabilities = ["SMS"] });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new SendMessagingMessageTool(),
            _harness.Build(authorize: MessagingToolHarness.Deny(ConversationOperation.Send)),
            new()
            {
                ["to"] = "+12015550123",
                ["fromAddressId"] = "line-1",
                ["body"] = "Hello",
            });

        // Assert
        Assert.StartsWith("Nothing was sent: this recipient already has a conversation you cannot send on", result);
        _harness.ConversationService.Verify(service => service.SendDirectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendBroadcast_QueuesOneBroadcastToTheRecipients()
    {
        // Arrange
        _harness.Endpoints.Add(new OmnichannelChannelEndpoint { ItemId = "line-1", DisplayText = "Support", Value = "+15550109000", Capabilities = ["SMS"] });
        _harness.AddContact("contact-1", "+12015550123");
        _harness.AddContact("contact-2", "+12015550124");

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendMessagingBroadcastTool(), _harness.Build(), new()
        {
            ["name"] = "Closed Friday",
            ["fromAddressId"] = "line-1",
            ["contactIds"] = new[] { "contact-1", "contact-2" },
            ["body"] = "The office is closed on Friday.",
        }));

        // Assert
        Assert.True(json.GetProperty("queued").GetBoolean());
        var broadcast = Assert.Single(_harness.Broadcasts.Created);
        Assert.Equal(MessagingBroadcastStatus.Queued, broadcast.Status);
        Assert.Equal("SMS", broadcast.Channel);
        Assert.Equal("+15550109000", broadcast.ServiceAddress);
        Assert.Equal(new[] { "+12015550123", "+12015550124" }, broadcast.Recipients);
        Assert.Equal("agent-1", broadcast.OwnerAgentId);
    }

    [Fact]
    public async Task UpdateConversation_ClosesItThroughTheConversationService()
    {
        // Arrange
        _harness.AddConversation();
        _harness.ConversationService.Setup(service => service.SetStatusAsync("conversation-1", ConversationStatus.Closed, It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingSendResult { Succeeded = true });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new UpdateMessagingConversationTool(), _harness.Build(), new()
        {
            ["conversationId"] = "conversation-1",
            ["action"] = "close",
        }));

        // Assert
        Assert.Equal("set to Closed", json.GetProperty("change").GetString());
    }

    [Fact]
    public async Task UpdateConversation_RefusesAClaimTheCallerMayNotMake()
    {
        // Arrange
        _harness.AddConversation();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new UpdateMessagingConversationTool(),
            _harness.Build(authorize: MessagingToolHarness.Deny(ConversationOperation.Claim)),
            new()
            {
                ["conversationId"] = "conversation-1",
                ["action"] = "claim",
            });

        // Assert
        Assert.Equal("Nothing was changed: You are not allowed to claim this conversation.", result);
        _harness.ConversationService.Verify(service => service.ClaimAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransferConversation_RefusesWhenTheCallerMayNotTransferIt()
    {
        // Arrange
        _harness.AddConversation();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(
            new TransferMessagingConversationTool(),
            _harness.Build(authorize: MessagingToolHarness.Deny(ConversationOperation.Transfer)),
            new()
            {
                ["conversationId"] = "conversation-1",
                ["targetType"] = "queue",
                ["targetId"] = "billing",
            });

        // Assert
        Assert.Equal("Nothing was transferred: You are not allowed to transfer this conversation.", result);
    }

    [Fact]
    public async Task SetAvailability_SetsTheCallersAvailability()
    {
        // Arrange
        _harness.Availability.Setup(service => service.SetAvailableAsync(It.IsAny<AgentProfile>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingAgentAvailability { Available = false });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SetMessagingAvailabilityTool(), _harness.Build(), new() { ["available"] = false }));

        // Assert
        Assert.False(json.GetProperty("available").GetBoolean());
        _harness.Availability.Verify(service => service.SetAvailableAsync(It.Is<AgentProfile>(agent => agent.ItemId == "agent-1"), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListBroadcasts_CountsTheRecipientsWithoutListingThem()
    {
        // Arrange
        _harness.Broadcasts.Add(new MessagingBroadcast
        {
            ItemId = "broadcast-1",
            Name = "Closed Friday",
            Recipients = ["+15550107788", "+15550107789"],
            SentCount = 1,
            SkippedCount = 1,
            Status = MessagingBroadcastStatus.Completed,
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListMessagingBroadcastsTool(), _harness.Build());

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());
        Assert.Equal(2, item.GetProperty("recipientCount").GetInt32());
        Assert.Equal(1, item.GetProperty("skippedCount").GetInt32());
        Assert.DoesNotContain("+15550107788", result);
    }

    [Fact]
    public async Task SendBroadcast_RefusesAnAddressThatCannotSendMessages()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendMessagingBroadcastTool(), _harness.Build(), new()
        {
            ["name"] = "Closed Friday",
            ["fromAddressId"] = "missing",
            ["contactIds"] = new[] { "contact-1" },
            ["body"] = "The office is closed on Friday.",
        });

        // Assert
        Assert.StartsWith("Nothing was queued: fromAddressId must be the ID of an address that can send messages", result);
        Assert.Empty(_harness.Broadcasts.Created);
    }

    [Fact]
    public async Task SendBroadcast_RefusesANumberTypedIntoTheChat()
    {
        // Arrange
        _harness.Endpoints.Add(new OmnichannelChannelEndpoint { ItemId = "line-1", DisplayText = "Support", Value = "+15550109000", Capabilities = ["SMS"] });
        _harness.AddContact("contact-1", "+12015550123");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendMessagingBroadcastTool(), _harness.Build(), new()
        {
            ["name"] = "Closed Friday",
            ["fromAddressId"] = "line-1",
            ["contactIds"] = new[] { "contact-1", "+12015550199" },
            ["body"] = "The office is closed on Friday.",
        });

        // Assert
        Assert.StartsWith("Nothing was queued: these are not IDs of CRM contacts: +12015550199.", result);
        Assert.Empty(_harness.Broadcasts.Created);
    }

    [Fact]
    public async Task SendBroadcast_RefusesAContactWithNoAddressOnTheChannel()
    {
        // Arrange
        _harness.Endpoints.Add(new OmnichannelChannelEndpoint { ItemId = "line-1", DisplayText = "Support", Value = "+15550109000", Capabilities = ["SMS"] });
        _harness.AddContact("contact-1", mobileNumber: null);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendMessagingBroadcastTool(), _harness.Build(), new()
        {
            ["name"] = "Closed Friday",
            ["fromAddressId"] = "line-1",
            ["contactIds"] = new[] { "contact-1" },
            ["body"] = "The office is closed on Friday.",
        });

        // Assert
        Assert.StartsWith("Nothing was queued: these contacts have no", result);
        Assert.Empty(_harness.Broadcasts.Created);
    }
}
