using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterRealTimeNotifierTests
{
    [Fact]
    public async Task NotifyOfferReceivedAsync_AutoOpenOffer_SendsNavigationOnlyToAssignedUser()
    {
        // Arrange
        var assignedUserClient = new Mock<IContactCenterHubClient>();
        var queueClient = new Mock<IContactCenterHubClient>();
        var supervisorClient = new Mock<IContactCenterHubClient>();
        var clients = new Mock<IHubClients<IContactCenterHubClient>>();

        clients.Setup(c => c.User("u1")).Returns(assignedUserClient.Object);
        clients.Setup(c => c.Group(ContactCenterHub.QueueGroup("q1"))).Returns(queueClient.Object);
        clients.Setup(c => c.Group(ContactCenterHub.SupervisorsGroup)).Returns(supervisorClient.Object);

        var hubContext = new Mock<IHubContext<ContactCenterHub, IContactCenterHubClient>>();
        hubContext.SetupGet(c => c.Clients).Returns(clients.Object);

        var notifier = new ContactCenterRealTimeNotifier(hubContext.Object);
        var notification = new AgentOfferNotification
        {
            UserId = "u1",
            AgentId = "a1",
            ActivityItemId = "act1",
            QueueId = "q1",
            AutoOpenActivity = true,
        };

        // Act
        await notifier.NotifyOfferReceivedAsync(notification, TestContext.Current.CancellationToken);

        // Assert
        assignedUserClient.Verify(
            client => client.OfferReceived(It.Is<AgentOfferNotification>(offer => offer.AutoOpenActivity)),
            Times.Once);
        queueClient.Verify(
            client => client.OfferReceived(It.Is<AgentOfferNotification>(offer => !offer.AutoOpenActivity)),
            Times.Once);
        supervisorClient.Verify(
            client => client.OfferReceived(It.Is<AgentOfferNotification>(offer => !offer.AutoOpenActivity)),
            Times.Once);
    }
}
