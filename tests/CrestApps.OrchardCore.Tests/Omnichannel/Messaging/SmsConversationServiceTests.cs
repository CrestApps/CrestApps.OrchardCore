using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class SmsConversationServiceTests
{
    [Fact]
    public async Task SendAsync_PersistsOutboundMessageAndAssignsClaim()
    {
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
        };

        OmnichannelMessage saved = null;
        var (service, dispatcher) = CreateService(conversation, dispatchSucceeds: true, onSave: m => saved = m);

        var result = await service.SendAsync(new MessagingSendRequest
        {
            ConversationId = "conv-1",
            Body = "Reply from agent",
            ActingAgentId = "agent-7",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(saved);
        Assert.False(saved.IsInbound);
        Assert.Equal("agent-7", saved.SentByAgentId);
        Assert.Equal(MessageDeliveryStatus.Sent.ToString(), saved.DeliveryStatus);
        Assert.Equal("conv-1", saved.ConversationId);

        // The reply claimed the unassigned personal thread for the acting agent.
        Assert.Equal("agent-7", conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
        Assert.True(conversation.IsRead);

        dispatcher.Verify(d => d.SendAsync(It.Is<SmsMessage>(m => m.From == "+15553334444" && m.To == "+15551112222"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_LeavesTheMessageQueuedForRetry_WhenTheProviderRefusesTheFirstAttempt()
    {
        // A provider that is briefly unreachable must not cost the agent their message. The bubble stays Queued
        // with a scheduled retry, and only becomes a visible failure once the backoff schedule is exhausted.
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
        };

        OmnichannelMessage saved = null;
        var (service, _) = CreateService(conversation, dispatchSucceeds: false, onSave: m => saved = m);

        var result = await service.SendAsync(new MessagingSendRequest { ConversationId = "conv-1", Body = "x", ActingAgentId = "agent-7" }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(MessageDeliveryStatus.Queued.ToString(), saved.DeliveryStatus);

        var state = saved.TryGet<OutboundDeliveryState>(out var deliveryState) ? deliveryState : null;

        Assert.Equal(1, state.Attempts);
        Assert.NotNull(state.NextAttemptUtc);
        Assert.Equal("provider down", state.LastError);
    }

    [Fact]
    public async Task SendAsync_WhenTheRecipientOptedOut_FailsAtOnce_InsteadOfRetrying()
    {
        // Arrange
        // A provider refusing a recipient who opted out will refuse every retry the same way; the bubble says so
        // now rather than after an hour of attempts that each count against the number with the carrier.
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
        };

        OmnichannelMessage saved = null;
        var (service, dispatcher) = CreateService(conversation, dispatchSucceeds: false, onSave: m => saved = m);
        var refusal = MessageDispatchResult.Failed("Attempt to send to unsubscribed recipient");
        refusal.ErrorCode = OmnichannelConstants.SmsErrorCodes.RecipientOptedOut;
        dispatcher.Setup(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(refusal);

        // Act
        var result = await service.SendAsync(new MessagingSendRequest { ConversationId = "conv-1", Body = "x", ActingAgentId = "agent-7" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(MessageDeliveryStatus.Failed.ToString(), saved.DeliveryStatus);
        Assert.True(saved.TryGet<OutboundDeliveryState>(out var state));
        Assert.Null(state.NextAttemptUtc);
    }

    [Fact]
    public async Task SendAsync_RecordsTheProviderMessageId_SoAReceiptMatchesTheRightBubble()
    {
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
        };

        OmnichannelMessage saved = null;
        var (service, _) = CreateService(conversation, dispatchSucceeds: true, onSave: m => saved = m);

        await service.SendAsync(new MessagingSendRequest { ConversationId = "conv-1", Body = "x", ActingAgentId = "agent-7" }, TestContext.Current.CancellationToken);

        Assert.Equal("provider-message-1", saved.ProviderMessageId);
        Assert.Equal(MessageDeliveryStatus.Sent.ToString(), saved.DeliveryStatus);
        Assert.Null(saved.TryGet<OutboundDeliveryState>(out var retried) ? retried.NextAttemptUtc : null);
    }

    [Fact]
    public async Task SendAsync_Refused_WhenContactHasOptedOut()
    {
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            AssignedAgentId = "agent-7",
            OwnerId = "agent-7",
            ContactContentItemId = "contact-1",
        };

        var optedOutContact = new ContentItem();
        optedOutContact.Alter<OmnichannelContactPart>(part => part.DoNotSms = true);

        var (service, dispatcher) = CreateService(conversation, dispatchSucceeds: true, onSave: _ => { }, contact: optedOutContact);

        var result = await service.SendAsync(new MessagingSendRequest { ConversationId = "conv-1", Body = "hi", ActingAgentId = "agent-7" }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        dispatcher.Verify(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_Refused_WhenAgentDoesNotOwnPersonalThread()
    {
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            AssignedAgentId = "agent-owner",
            OwnerId = "agent-owner",
        };

        var (service, dispatcher) = CreateService(
            conversation,
            dispatchSucceeds: true,
            onSave: _ => { },
            conversationAuthorized: false);

        var result = await service.SendAsync(
            new MessagingSendRequest
            {
                ConversationId = "conv-1",
                Body = "hi",
                ActingAgentId = "agent-intruder",
                Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-intruder")], "Test")),
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        dispatcher.Verify(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_Allowed_WhenNoPrincipalIsSupplied_ForSystemSends()
    {
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            AssignedAgentId = "agent-owner",
            OwnerId = "agent-owner",
        };

        var (service, dispatcher) = CreateService(
            conversation,
            dispatchSucceeds: true,
            onSave: _ => { },
            conversationAuthorized: false);

        var result = await service.SendAsync(
            new MessagingSendRequest { ConversationId = "conv-1", Body = "auto reply" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        dispatcher.Verify(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ToAnUnassignedQueueThread_ClaimsItForTheSender()
    {
        // Arrange
        // A handed-off thread sits unassigned in its queue. The agent who answers it has taken it: leaving it
        // unclaimed kept offering it to the rest of the department, still showing "Claim", while they talked.
        var conversation = CreateQueueConversation(assignedAgentId: null, ConversationAssignmentStatus.Unassigned);
        var notifier = new Mock<IMessagingRealTimeNotifier>();
        var (service, dispatcher) = CreateService(
            conversation,
            dispatchSucceeds: true,
            onSave: _ => { },
            conversationAuthorization: CreateQueueMemberAuthorization(),
            notifier: notifier);

        // Act
        var result = await service.SendAsync(
            new MessagingSendRequest { ConversationId = "conv-1", Body = "nice! thank you", ActingAgentId = "agent-7", Principal = CreateQueueMemberPrincipal() },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("agent-7", conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
        Assert.Equal(ConversationOwnerType.Queue, conversation.OwnerType);
        Assert.Equal("queue-1", conversation.OwnerId); // the queue stays the owner, as with Claim
        dispatcher.Verify(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(
            n => n.ConversationAssignedAsync(It.Is<MessagingAssignmentNotification>(a => a.AssignedAgentId == "agent-7" && a.OwnerQueueId == "queue-1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_ToAQueueThreadAColleagueClaimed_IsRefused_AndKeepsTheirClaim()
    {
        // Arrange
        var conversation = CreateQueueConversation(assignedAgentId: "agent-owner", ConversationAssignmentStatus.Assigned);
        var (service, dispatcher) = CreateService(
            conversation,
            dispatchSucceeds: true,
            onSave: _ => { },
            conversationAuthorization: CreateQueueMemberAuthorization());

        // Act
        var result = await service.SendAsync(
            new MessagingSendRequest { ConversationId = "conv-1", Body = "hi", ActingAgentId = "agent-7", Principal = CreateQueueMemberPrincipal() },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("agent-owner", conversation.AssignedAgentId);
        dispatcher.Verify(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_BySomeoneAllowedToAnswerAColleaguesThread_DoesNotTakeItFromThem()
    {
        // Arrange
        // A supervisor may reply on any thread, but replying on one an agent already holds is not a claim.
        var conversation = CreateQueueConversation(assignedAgentId: "agent-owner", ConversationAssignmentStatus.Assigned);
        var notifier = new Mock<IMessagingRealTimeNotifier>();
        var (service, _) = CreateService(conversation, dispatchSucceeds: true, onSave: _ => { }, conversationAuthorized: true, notifier: notifier);

        // Act
        var result = await service.SendAsync(
            new MessagingSendRequest { ConversationId = "conv-1", Body = "hi", ActingAgentId = "agent-supervisor", Principal = CreateQueueMemberPrincipal() },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("agent-owner", conversation.AssignedAgentId);
        notifier.Verify(n => n.ConversationAssignedAsync(It.IsAny<MessagingAssignmentNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_ToAPersonalThreadAnotherAgentOwns_DoesNotTakeItFromThem()
    {
        // Arrange
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = "agent-owner",
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
        };

        var (service, _) = CreateService(conversation, dispatchSucceeds: true, onSave: _ => { }, conversationAuthorized: true);

        // Act
        await service.SendAsync(
            new MessagingSendRequest { ConversationId = "conv-1", Body = "hi", ActingAgentId = "agent-supervisor", Principal = CreateQueueMemberPrincipal() },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("agent-owner", conversation.OwnerId);
        Assert.Null(conversation.AssignedAgentId);
    }

    private static MessagingConversation CreateQueueConversation(string assignedAgentId, ConversationAssignmentStatus assignmentStatus)
        => new()
        {
            Channel = "SMS",
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "queue-1",
            AssignedAgentId = assignedAgentId,
            AssignmentStatus = assignmentStatus,
        };

    private static ClaimsPrincipal CreateQueueMemberPrincipal()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-7")], "Test"));

    // The real conversation rule for a queue member who is not a supervisor.
    private static MessagingConversationAuthorizationService CreateQueueMemberAuthorization()
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        var agentProfileManager = new Mock<IAgentProfileManager>();
        agentProfileManager
            .Setup(manager => manager.FindByUserIdAsync("user-7", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-7", UserId = "user-7", QueueIds = ["queue-1"], AllowedQueueIds = ["queue-1"] });

        return new MessagingConversationAuthorizationService(
            authorizationService.Object,
            agentProfileManager.Object,
            new PermissiveAgentEntitlementPolicy());
    }

    private static (MessagingConversationService Service, Mock<ISmsDispatcher> Dispatcher) CreateService(
        MessagingConversation conversation,
        bool dispatchSucceeds,
        Action<OmnichannelMessage> onSave,
        ContentItem contact = null,
        bool conversationAuthorized = true,
        IMessagingConversationAuthorizationService conversationAuthorization = null,
        Mock<IMessagingRealTimeNotifier> notifier = null)
    {
        var store = new Mock<IMessagingConversationStore>();
        store.Setup(s => s.FindByIdAsync(conversation.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        store.Setup(s => s.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var dispatcher = new Mock<ISmsDispatcher>();
        dispatcher.Setup(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispatchSucceeds
                ? MessageDispatchResult.Success("provider-message-1")
                : MessageDispatchResult.Failed("provider down"));

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync(contact);

        notifier ??= new Mock<IMessagingRealTimeNotifier>();

        var session = new Mock<ISession>();
        session.Setup(s => s.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(new InvocationAction(inv => onSave((OmnichannelMessage)inv.Arguments[0])));

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        var contactResolver = new Mock<IMessagingContactResolver>();
        contactResolver.Setup(r => r.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<string>(null));

        var service = new MessagingConversationService(
            store.Object,
            MessagingTestChannels.Resolver(dispatcher.Object),
            contentManager.Object,
            contactResolver.Object,
            notifier.Object,
            conversationAuthorization ?? CreateConversationAuthorizationService(conversationAuthorized),
            session.Object,
            new NoOpSmsFirstResponseSlaService(),
            clock.Object,
            NullLogger<MessagingConversationService>.Instance);

        return (service, dispatcher);
    }

    private static IMessagingConversationAuthorizationService CreateConversationAuthorizationService(bool authorized)
    {
        var authorizationService = new Mock<IMessagingConversationAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<MessagingConversation>(),
                It.IsAny<ConversationOperation>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorized);

        return authorizationService.Object;
    }
}
