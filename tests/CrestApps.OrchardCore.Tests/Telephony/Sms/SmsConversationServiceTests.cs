using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using CrestApps.OrchardCore.Sms.Workspace.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsConversationServiceTests
{
    [Fact]
    public async Task SendAsync_PersistsOutboundMessageAndAssignsClaim()
    {
        var conversation = new SmsConversation
        {
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            CustomerAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            AssignmentStatus = SmsConversationAssignmentStatus.Unassigned,
        };

        OmnichannelMessage saved = null;
        var (service, dispatcher) = CreateService(conversation, dispatchSucceeds: true, onSave: m => saved = m);

        var result = await service.SendAsync(new SmsSendRequest
        {
            ConversationId = "conv-1",
            Body = "Reply from agent",
            ActingAgentId = "agent-7",
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(saved);
        Assert.False(saved.IsInbound);
        Assert.Equal("agent-7", saved.SentByAgentId);
        Assert.Equal(SmsDeliveryStatus.Sent.ToString(), saved.DeliveryStatus);
        Assert.Equal("conv-1", saved.ConversationId);

        // The reply claimed the unassigned personal thread for the acting agent.
        Assert.Equal("agent-7", conversation.AssignedAgentId);
        Assert.Equal(SmsConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
        Assert.True(conversation.IsRead);

        dispatcher.Verify(d => d.SendAsync(It.Is<SmsMessage>(m => m.From == "+15553334444" && m.To == "+15551112222"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_MarksMessageFailed_WhenDispatchFails()
    {
        var conversation = new SmsConversation
        {
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            CustomerAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            AssignmentStatus = SmsConversationAssignmentStatus.Unassigned,
        };

        OmnichannelMessage saved = null;
        var (service, _) = CreateService(conversation, dispatchSucceeds: false, onSave: m => saved = m);

        var result = await service.SendAsync(new SmsSendRequest { ConversationId = "conv-1", Body = "x", ActingAgentId = "agent-7" });

        Assert.False(result.Succeeded);
        Assert.Equal(SmsDeliveryStatus.Failed.ToString(), saved.DeliveryStatus);
    }

    [Fact]
    public async Task SendAsync_Refused_WhenContactHasOptedOut()
    {
        var conversation = new SmsConversation
        {
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            CustomerAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            AssignedAgentId = "agent-7",
            OwnerId = "agent-7",
            ContactContentItemId = "contact-1",
        };

        var optedOutContact = new ContentItem();
        optedOutContact.Alter<OmnichannelContactPart>(part => part.DoNotSms = true);

        var (service, dispatcher) = CreateService(conversation, dispatchSucceeds: true, onSave: _ => { }, contact: optedOutContact);

        var result = await service.SendAsync(new SmsSendRequest { ConversationId = "conv-1", Body = "hi", ActingAgentId = "agent-7" });

        Assert.False(result.Succeeded);
        dispatcher.Verify(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_Refused_WhenAgentDoesNotOwnPersonalThread()
    {
        var conversation = new SmsConversation
        {
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            CustomerAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            AssignedAgentId = "agent-owner",
            OwnerId = "agent-owner",
        };

        var (service, dispatcher) = CreateService(conversation, dispatchSucceeds: true, onSave: _ => { });

        var result = await service.SendAsync(new SmsSendRequest { ConversationId = "conv-1", Body = "hi", ActingAgentId = "agent-intruder" });

        Assert.False(result.Succeeded);
        dispatcher.Verify(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (SmsConversationService Service, Mock<ISmsDispatcher> Dispatcher) CreateService(
        SmsConversation conversation,
        bool dispatchSucceeds,
        Action<OmnichannelMessage> onSave,
        ContentItem contact = null)
    {
        var store = new Mock<ISmsConversationStore>();
        store.Setup(s => s.FindByIdAsync(conversation.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        store.Setup(s => s.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var dispatcher = new Mock<ISmsDispatcher>();
        dispatcher.Setup(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispatchSucceeds
                ? Result.Success()
                : Result.Failed(new LocalizedString("err", "provider down")));

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync(contact);

        var notifier = new Mock<ISmsRealTimeNotifier>();

        var session = new Mock<ISession>();
        session.Setup(s => s.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(new InvocationAction(inv => onSave((OmnichannelMessage)inv.Arguments[0])));

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        var contactResolver = new Mock<ISmsContactResolver>();
        contactResolver.Setup(r => r.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<string>(null));

        var service = new SmsConversationService(
            store.Object,
            dispatcher.Object,
            contentManager.Object,
            contactResolver.Object,
            notifier.Object,
            session.Object,
            clock.Object,
            RedactorProviderFactory.Create(),
            NullLogger<SmsConversationService>.Instance);

        return (service, dispatcher);
    }
}
