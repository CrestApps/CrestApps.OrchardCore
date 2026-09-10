using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Hubs;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public sealed class SmsRealTimeNotifierTests
{
    [Fact]
    public async Task MessageDeliveryUpdatedAsync_WhenAssigned_ReachesOnlyTheTenantQualifiedAgentGroup()
    {
        // Arrange
        var tenantAAgentClient = new Mock<ISmsPortalHubClient>();
        var tenantBAgentClient = new Mock<ISmsPortalHubClient>();
        var allClient = new Mock<ISmsPortalHubClient>();
        var clients = new Mock<IHubClients<ISmsPortalHubClient>>();

        clients
            .Setup(hubClients => hubClients.Group(TenantSignalRGroupName.ForGroup("TenantA", SmsPortalHub.AgentGroup("agent-1"))))
            .Returns(tenantAAgentClient.Object);
        clients
            .Setup(hubClients => hubClients.Group(TenantSignalRGroupName.ForGroup("TenantB", SmsPortalHub.AgentGroup("agent-1"))))
            .Returns(tenantBAgentClient.Object);
        clients.SetupGet(hubClients => hubClients.All).Returns(allClient.Object);

        var hubContext = new Mock<IHubContext<SmsPortalHub, ISmsPortalHubClient>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var notifier = new SmsRealTimeNotifier(hubContext.Object, new ShellSettings { Name = "TenantA" });

        var notification = new SmsDeliveryNotification
        {
            ConversationId = "conv-1",
            MessageId = "message-1",
            Status = SmsDeliveryStatus.Delivered,
            AssignedAgentId = "agent-1",
        };

        // Act
        await notifier.MessageDeliveryUpdatedAsync(notification, TestContext.Current.CancellationToken);

        // Assert
        tenantAAgentClient.Verify(client => client.MessageDeliveryUpdated(notification), Times.Once);
        tenantBAgentClient.Verify(client => client.MessageDeliveryUpdated(It.IsAny<SmsDeliveryNotification>()), Times.Never);
        allClient.Verify(client => client.MessageDeliveryUpdated(It.IsAny<SmsDeliveryNotification>()), Times.Never);
    }

    [Fact]
    public async Task MessageDeliveryUpdatedAsync_WhenQueueOwnedAndUnassigned_ReachesTheQueueGroup()
    {
        // Arrange
        var queueClient = new Mock<ISmsPortalHubClient>();
        var allClient = new Mock<ISmsPortalHubClient>();
        var clients = new Mock<IHubClients<ISmsPortalHubClient>>();

        clients
            .Setup(hubClients => hubClients.Group(TenantSignalRGroupName.ForGroup("TenantA", SmsPortalHub.QueueGroup("queue-1"))))
            .Returns(queueClient.Object);
        clients.SetupGet(hubClients => hubClients.All).Returns(allClient.Object);

        var hubContext = new Mock<IHubContext<SmsPortalHub, ISmsPortalHubClient>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var notifier = new SmsRealTimeNotifier(hubContext.Object, new ShellSettings { Name = "TenantA" });

        var notification = new SmsDeliveryNotification
        {
            ConversationId = "conv-1",
            MessageId = "message-1",
            Status = SmsDeliveryStatus.Failed,
            OwnerQueueId = "queue-1",
        };

        // Act
        await notifier.MessageDeliveryUpdatedAsync(notification, TestContext.Current.CancellationToken);

        // Assert
        queueClient.Verify(client => client.MessageDeliveryUpdated(notification), Times.Once);
        allClient.Verify(client => client.MessageDeliveryUpdated(It.IsAny<SmsDeliveryNotification>()), Times.Never);
    }

    [Fact]
    public async Task MessageDeliveryUpdatedAsync_WhenNeitherAssignedNorQueueOwned_ReachesTheUnassignedGroup()
    {
        // Arrange
        var unassignedClient = new Mock<ISmsPortalHubClient>();
        var allClient = new Mock<ISmsPortalHubClient>();
        var clients = new Mock<IHubClients<ISmsPortalHubClient>>();

        clients
            .Setup(hubClients => hubClients.Group(TenantSignalRGroupName.ForGroup("TenantA", SmsPortalHub.UnassignedGroup)))
            .Returns(unassignedClient.Object);
        clients.SetupGet(hubClients => hubClients.All).Returns(allClient.Object);

        var hubContext = new Mock<IHubContext<SmsPortalHub, ISmsPortalHubClient>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);

        var notifier = new SmsRealTimeNotifier(hubContext.Object, new ShellSettings { Name = "TenantA" });

        var notification = new SmsDeliveryNotification
        {
            ConversationId = "conv-1",
            MessageId = "message-1",
            Status = SmsDeliveryStatus.Sent,
        };

        // Act
        await notifier.MessageDeliveryUpdatedAsync(notification, TestContext.Current.CancellationToken);

        // Assert
        unassignedClient.Verify(client => client.MessageDeliveryUpdated(notification), Times.Once);
        allClient.Verify(client => client.MessageDeliveryUpdated(It.IsAny<SmsDeliveryNotification>()), Times.Never);
    }
}
