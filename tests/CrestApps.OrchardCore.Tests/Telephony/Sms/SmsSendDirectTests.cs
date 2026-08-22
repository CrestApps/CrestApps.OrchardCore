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

public class SmsSendDirectTests
{
    [Fact]
    public async Task SendDirectAsync_ReusesExistingConversationForTheCustomer_InsteadOfCreating()
    {
        var existing = new SmsConversation
        {
            ItemId = "conv-existing",
            ServiceAddress = "+15553330000",
            CustomerAddress = "+15551112222",
            Status = SmsConversationStatus.Closed,
        };

        var (service, store, dispatcher) = CreateService(existing);

        // The agent composes from a different DID than the existing thread runs on.
        var result = await service.SendDirectAsync("+15559998888", "+15551112222", "hi again", "agent-1");

        Assert.True(result.Succeeded);
        store.Verify(s => s.CreateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(s => s.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(SmsConversationStatus.Open, existing.Status); // reopened
        Assert.Equal("conv-existing", result.Message.ConversationId);
        // Sends from the existing conversation's number, not the composed-from number.
        dispatcher.Verify(d => d.SendAsync(It.Is<SmsMessage>(m => m.From == "+15553330000"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendDirectAsync_CreatesConversation_WhenNoneExistsForTheCustomer()
    {
        var (service, store, _) = CreateService(existing: null);

        var result = await service.SendDirectAsync("+15559998888", "+15551112222", "hello", "agent-1");

        Assert.True(result.Succeeded);
        store.Verify(s => s.CreateAsync(It.Is<SmsConversation>(c => c.CustomerAddress == "+15551112222" && c.ServiceAddress == "+15559998888"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (SmsConversationService Service, Mock<ISmsConversationStore> Store, Mock<ISmsDispatcher> Dispatcher) CreateService(SmsConversation existing)
    {
        var store = new Mock<ISmsConversationStore>();
        store.Setup(s => s.FindByCustomerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        store.Setup(s => s.CreateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        store.Setup(s => s.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var dispatcher = new Mock<ISmsDispatcher>();
        dispatcher.Setup(d => d.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var contactResolver = new Mock<ISmsContactResolver>();
        contactResolver.Setup(r => r.ResolveContactContentItemIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<string>(null));

        var session = new Mock<ISession>();
        session.Setup(s => s.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        var service = new SmsConversationService(
            store.Object,
            dispatcher.Object,
            new Mock<IContentManager>().Object,
            contactResolver.Object,
            new Mock<ISmsRealTimeNotifier>().Object,
            session.Object,
            clock.Object,
            RedactorProviderFactory.Create(),
            NullLogger<SmsConversationService>.Instance);

        return (service, store, dispatcher);
    }
}
