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
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Sms;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class SmsSendDirectTests
{
    [Fact]
    public async Task SendDirectAsync_ReusesExistingConversationForTheContact_InsteadOfCreating()
    {
        var existing = new MessagingConversation
        {
            Channel = "SMS",
            ItemId = "conv-existing",
            ServiceAddress = "+15553330000",
            ContactAddress = "+15551112222",
            Status = ConversationStatus.Closed,
        };

        var (service, store, dispatcher) = CreateService(existing);

        // The agent composes from a different DID than the existing thread runs on.
        var result = await service.SendDirectAsync("SMS", "+15559998888", "+15551112222", "hi again", "agent-1", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        store.Verify(s => s.CreateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(s => s.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ConversationStatus.Open, existing.Status); // reopened
        Assert.Equal("conv-existing", result.Message.ConversationId);
        // Sends from the existing conversation's number, not the composed-from number.
        dispatcher.Verify(d => d.SendAsync(It.Is<SmsMessage>(m => m.From == "+15553330000"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendDirectAsync_CreatesConversation_WhenNoneExistsForTheContact()
    {
        var (service, store, _) = CreateService(existing: null);

        var result = await service.SendDirectAsync("SMS", "+15559998888", "+15551112222", "hello", "agent-1", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        store.Verify(s => s.CreateAsync(It.Is<MessagingConversation>(c => c.ContactAddress == "+15551112222" && c.ServiceAddress == "+15559998888"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (MessagingConversationService Service, Mock<IMessagingConversationStore> Store, Mock<ISmsDispatcher> Dispatcher) CreateService(MessagingConversation existing)
    {
        var store = new Mock<IMessagingConversationStore>();
        store.Setup(s => s.FindByContactAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        store.Setup(s => s.CreateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        store.Setup(s => s.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var dispatcher = new Mock<ISmsDispatcher>();
        dispatcher.Setup(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(MessageDispatchResult.Success("provider-message-1"));

        var contactResolver = new Mock<IMessagingContactResolver>();
        contactResolver.Setup(r => r.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<string>(null));

        var session = new Mock<ISession>();
        session.Setup(s => s.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        var service = new MessagingConversationService(
            store.Object,
            MessagingTestChannels.Resolver(dispatcher.Object),
            new Mock<IContentManager>().Object,
            contactResolver.Object,
            new Mock<IMessagingRealTimeNotifier>().Object,
            Mock.Of<IMessagingConversationAuthorizationService>(),
            session.Object,
            new NoOpSmsFirstResponseSlaService(),
            clock.Object,
            NullLogger<MessagingConversationService>.Instance);

        return (service, store, dispatcher);
    }
}
