using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Hubs;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public sealed class SmsRealTimeNotifierTests
{
    [Fact]
    public async Task MessageDeliveryUpdatedAsync_WhenAssigned_ReachesOnlyTheTenantQualifiedAgentGroup()
    {
        // Arrange
        var tenantAAgentClient = new Mock<IMessagingHubClient>();
        var tenantBAgentClient = new Mock<IMessagingHubClient>();
        var allClient = new Mock<IMessagingHubClient>();
        var clients = new Mock<IHubClients<IMessagingHubClient>>();

        clients
            .Setup(hubClients => hubClients.Group(TenantSignalRGroupName.ForGroup("TenantA", MessagingHub.AgentGroup("agent-1"))))
            .Returns(tenantAAgentClient.Object);
        clients
            .Setup(hubClients => hubClients.Group(TenantSignalRGroupName.ForGroup("TenantB", MessagingHub.AgentGroup("agent-1"))))
            .Returns(tenantBAgentClient.Object);
        clients.SetupGet(hubClients => hubClients.All).Returns(allClient.Object);

        var hubContext = new Mock<IHubContext<MessagingHub, IMessagingHubClient>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var notifier = new MessagingRealTimeNotifier(hubContext.Object, new ShellSettings { Name = "TenantA" });

        var notification = new MessagingDeliveryNotification
        {
            ConversationId = "conv-1",
            MessageId = "message-1",
            Status = MessageDeliveryStatus.Delivered,
            AssignedAgentId = "agent-1",
        };

        // Act
        await notifier.MessageDeliveryUpdatedAsync(notification, TestContext.Current.CancellationToken);

        // Assert
        tenantAAgentClient.Verify(client => client.MessageDeliveryUpdated(notification), Times.Once);
        tenantBAgentClient.Verify(client => client.MessageDeliveryUpdated(It.IsAny<MessagingDeliveryNotification>()), Times.Never);
        allClient.Verify(client => client.MessageDeliveryUpdated(It.IsAny<MessagingDeliveryNotification>()), Times.Never);
    }

    [Fact]
    public async Task MessageDeliveryUpdatedAsync_WhenQueueOwnedAndUnassigned_ReachesTheQueueGroup()
    {
        // Arrange
        var queueClient = new Mock<IMessagingHubClient>();
        var allClient = new Mock<IMessagingHubClient>();
        var clients = new Mock<IHubClients<IMessagingHubClient>>();

        clients
            .Setup(hubClients => hubClients.Group(TenantSignalRGroupName.ForGroup("TenantA", MessagingHub.QueueGroup("queue-1"))))
            .Returns(queueClient.Object);
        clients.SetupGet(hubClients => hubClients.All).Returns(allClient.Object);

        var hubContext = new Mock<IHubContext<MessagingHub, IMessagingHubClient>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var notifier = new MessagingRealTimeNotifier(hubContext.Object, new ShellSettings { Name = "TenantA" });

        var notification = new MessagingDeliveryNotification
        {
            ConversationId = "conv-1",
            MessageId = "message-1",
            Status = MessageDeliveryStatus.Failed,
            OwnerQueueId = "queue-1",
        };

        // Act
        await notifier.MessageDeliveryUpdatedAsync(notification, TestContext.Current.CancellationToken);

        // Assert
        queueClient.Verify(client => client.MessageDeliveryUpdated(notification), Times.Once);
        allClient.Verify(client => client.MessageDeliveryUpdated(It.IsAny<MessagingDeliveryNotification>()), Times.Never);
    }

    [Fact]
    public async Task MessageDeliveryUpdatedAsync_WhenNeitherAssignedNorQueueOwned_ReachesTheUnassignedGroup()
    {
        // Arrange
        var unassignedClient = new Mock<IMessagingHubClient>();
        var allClient = new Mock<IMessagingHubClient>();
        var clients = new Mock<IHubClients<IMessagingHubClient>>();

        clients
            .Setup(hubClients => hubClients.Group(TenantSignalRGroupName.ForGroup("TenantA", MessagingHub.UnassignedGroup)))
            .Returns(unassignedClient.Object);
        clients.SetupGet(hubClients => hubClients.All).Returns(allClient.Object);

        var hubContext = new Mock<IHubContext<MessagingHub, IMessagingHubClient>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var notifier = new MessagingRealTimeNotifier(hubContext.Object, new ShellSettings { Name = "TenantA" });

        var notification = new MessagingDeliveryNotification
        {
            ConversationId = "conv-1",
            MessageId = "message-1",
            Status = MessageDeliveryStatus.Sent,
        };

        // Act
        await notifier.MessageDeliveryUpdatedAsync(notification, TestContext.Current.CancellationToken);

        // Assert
        unassignedClient.Verify(client => client.MessageDeliveryUpdated(notification), Times.Once);
        allClient.Verify(client => client.MessageDeliveryUpdated(It.IsAny<MessagingDeliveryNotification>()), Times.Never);
    }
}
