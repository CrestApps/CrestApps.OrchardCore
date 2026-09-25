using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
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

    private static (MessagingConversationService Service, Mock<ISmsDispatcher> Dispatcher) CreateService(
        MessagingConversation conversation,
        bool dispatchSucceeds,
        Action<OmnichannelMessage> onSave,
        ContentItem contact = null,
        bool conversationAuthorized = true)
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

        var notifier = new Mock<IMessagingRealTimeNotifier>();

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
            CreateConversationAuthorizationService(conversationAuthorized),
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
