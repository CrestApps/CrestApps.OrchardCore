using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class SmsBroadcastServiceTests
{
    [Fact]
    public async Task ProcessAsync_SendsEachRecipientOnce_AndCompletes()
    {
        var broadcast = new MessagingBroadcast
        {
            ItemId = "bc-1",
            Channel = "SMS",
            ServiceAddress = "+15553334444",
            Body = "Hello everyone",
            Recipients = ["+15551110001", "+15551110002", "+15551110003"],
            Status = MessagingBroadcastStatus.Queued,
        };

        var sentTo = new List<string>();
        var conversationService = new Mock<IMessagingConversationService>();
        conversationService
            .Setup(s => s.SendDirectAsync(broadcast.Channel, broadcast.ServiceAddress, It.IsAny<string>(), broadcast.Body, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string to, string _, string _, CancellationToken _) =>
            {
                sentTo.Add(to);
                return new MessagingSendResult { Succeeded = true };
            });

        var service = CreateService(conversationService.Object);

        await service.ProcessAsync(broadcast, TestContext.Current.CancellationToken);

        Assert.Equal(3, broadcast.SentCount);
        Assert.Equal(0, broadcast.FailedCount);
        Assert.Equal(MessagingBroadcastStatus.Completed, broadcast.Status);
        Assert.NotNull(broadcast.CompletedUtc);
        Assert.Equal(3, broadcast.ProcessedRecipients.Count);
        Assert.Equal(["+15551110001", "+15551110002", "+15551110003"], sentTo);
    }

    [Fact]
    public async Task ProcessAsync_SkipsAlreadyProcessedRecipients_OnResume()
    {
        var broadcast = new MessagingBroadcast
        {
            ItemId = "bc-1",
            Channel = "SMS",
            ServiceAddress = "+15553334444",
            Body = "Hello again",
            Recipients = ["+15551110001", "+15551110002", "+15551110003"],
            ProcessedRecipients = ["+15551110001", "+15551110002"],
            SentCount = 2,
            Status = MessagingBroadcastStatus.Running,
        };

        var sentTo = new List<string>();
        var conversationService = new Mock<IMessagingConversationService>();
        conversationService
            .Setup(s => s.SendDirectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string to, string _, string _, CancellationToken _) =>
            {
                sentTo.Add(to);
                return new MessagingSendResult { Succeeded = true };
            });

        var service = CreateService(conversationService.Object);

        await service.ProcessAsync(broadcast, TestContext.Current.CancellationToken);

        // Only the third recipient is sent on resume.
        Assert.Equal(["+15551110003"], sentTo);
        Assert.Equal(3, broadcast.SentCount);
        Assert.Equal(MessagingBroadcastStatus.Completed, broadcast.Status);
    }

    [Fact]
    public async Task ProcessAsync_CountsFailures()
    {
        var broadcast = new MessagingBroadcast
        {
            ItemId = "bc-1",
            Channel = "SMS",
            ServiceAddress = "+15553334444",
            Body = "Hi",
            Recipients = ["+15551110001", "+15551110002"],
            Status = MessagingBroadcastStatus.Queued,
        };

        var conversationService = new Mock<IMessagingConversationService>();
        conversationService
            .Setup(s => s.SendDirectAsync(It.IsAny<string>(), It.IsAny<string>(), "+15551110001", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingSendResult { Succeeded = true });
        conversationService
            .Setup(s => s.SendDirectAsync(It.IsAny<string>(), It.IsAny<string>(), "+15551110002", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MessagingSendResult.Failed("blocked"));

        var service = CreateService(conversationService.Object);

        await service.ProcessAsync(broadcast, TestContext.Current.CancellationToken);

        Assert.Equal(1, broadcast.SentCount);
        Assert.Equal(1, broadcast.FailedCount);
        Assert.Equal(MessagingBroadcastStatus.Completed, broadcast.Status);
    }

    private static MessagingBroadcastService CreateService(IMessagingConversationService conversationService)
    {
        var store = new Mock<IMessagingBroadcastStore>();
        store.Setup(s => s.UpdateAsync(It.IsAny<MessagingBroadcast>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

        return new MessagingBroadcastService(store.Object, conversationService, clock.Object, NullLogger<MessagingBroadcastService>.Instance);
    }
}
