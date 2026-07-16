using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.SignalR;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterRealTimeNotifierTests
{
    [Fact]
    public async Task NotifyPresenceChangedAsync_TwoShells_UseDistinctTenantQualifiedDestinations()
    {
        // Arrange
        var tenantAUserClient = new Mock<IContactCenterHubClient>();
        var tenantASupervisorClient = new Mock<IContactCenterHubClient>();
        var tenantBUserClient = new Mock<IContactCenterHubClient>();
        var tenantBSupervisorClient = new Mock<IContactCenterHubClient>();
        var clients = new Mock<IHubClients<IContactCenterHubClient>>();
        var tenantAUserGroup = TenantSignalRGroupName.ForUser("TenantA", "u1");
        var tenantASupervisorsGroup = TenantSignalRGroupName.ForGroup("TenantA", ContactCenterHub.SupervisorsGroup);
        var tenantBUserGroup = TenantSignalRGroupName.ForUser("TenantB", "u1");
        var tenantBSupervisorsGroup = TenantSignalRGroupName.ForGroup("TenantB", ContactCenterHub.SupervisorsGroup);

        clients.Setup(c => c.Group(tenantAUserGroup)).Returns(tenantAUserClient.Object);
        clients.Setup(c => c.Group(tenantASupervisorsGroup)).Returns(tenantASupervisorClient.Object);
        clients.Setup(c => c.Group(tenantBUserGroup)).Returns(tenantBUserClient.Object);
        clients.Setup(c => c.Group(tenantBSupervisorsGroup)).Returns(tenantBSupervisorClient.Object);

        var hubContext = new Mock<IHubContext<ContactCenterHub, IContactCenterHubClient>>();
        hubContext.SetupGet(c => c.Clients).Returns(clients.Object);

        var sessionManager = new Mock<IAgentSessionManager>().Object;
        var tenantANotifier = new ContactCenterRealTimeNotifier(
            hubContext.Object,
            sessionManager,
            new ShellSettings { Name = "TenantA" });
        var tenantBNotifier = new ContactCenterRealTimeNotifier(
            hubContext.Object,
            sessionManager,
            new ShellSettings { Name = "TenantB" });
        var tenantANotification = new AgentPresenceNotification
        {
            UserId = "u1",
            AgentId = "agent-a",
        };
        var tenantBNotification = new AgentPresenceNotification
        {
            UserId = "u1",
            AgentId = "agent-b",
        };

        // Act
        await tenantANotifier.NotifyPresenceChangedAsync(
            tenantANotification,
            TestContext.Current.CancellationToken);
        await tenantBNotifier.NotifyPresenceChangedAsync(
            tenantBNotification,
            TestContext.Current.CancellationToken);

        // Assert
        tenantAUserClient.Verify(c => c.PresenceChanged(tenantANotification), Times.Once);
        tenantASupervisorClient.Verify(c => c.PresenceChanged(tenantANotification), Times.Once);
        tenantBUserClient.Verify(c => c.PresenceChanged(tenantBNotification), Times.Once);
        tenantBSupervisorClient.Verify(c => c.PresenceChanged(tenantBNotification), Times.Once);
        tenantAUserClient.Verify(c => c.PresenceChanged(tenantBNotification), Times.Never);
        tenantASupervisorClient.Verify(c => c.PresenceChanged(tenantBNotification), Times.Never);
        tenantBUserClient.Verify(c => c.PresenceChanged(tenantANotification), Times.Never);
        tenantBSupervisorClient.Verify(c => c.PresenceChanged(tenantANotification), Times.Never);
        clients.Verify(c => c.Group(ContactCenterHub.SupervisorsGroup), Times.Never);
    }

    [Fact]
    public async Task NotifyOfferReceivedAsync_AutoOpenOffer_SendsNavigationOnlyToAssignedUser()
    {
        // Arrange
        var assignedUserClient = new Mock<IContactCenterHubClient>();
        var queueClient = new Mock<IContactCenterHubClient>();
        var supervisorClient = new Mock<IContactCenterHubClient>();
        var clients = new Mock<IHubClients<IContactCenterHubClient>>();
        var shellSettings = new ShellSettings
        {
            Name = "TenantA",
        };
        var userGroup = TenantSignalRGroupName.ForUser(shellSettings.Name, "u1");
        var queueGroup = TenantSignalRGroupName.ForGroup(shellSettings.Name, ContactCenterHub.QueueGroup("q1"));
        var supervisorsGroup = TenantSignalRGroupName.ForGroup(shellSettings.Name, ContactCenterHub.SupervisorsGroup);

        clients.Setup(c => c.Group(userGroup)).Returns(assignedUserClient.Object);
        clients.Setup(c => c.Group(queueGroup)).Returns(queueClient.Object);
        clients.Setup(c => c.Group(supervisorsGroup)).Returns(supervisorClient.Object);

        var hubContext = new Mock<IHubContext<ContactCenterHub, IContactCenterHubClient>>();
        hubContext.SetupGet(c => c.Clients).Returns(clients.Object);

        var notifier = new ContactCenterRealTimeNotifier(
            hubContext.Object,
            new Mock<IAgentSessionManager>().Object,
            shellSettings);
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

    [Fact]
    public async Task NotifyAgentMembershipChangedAsync_RemovesEveryConnectionFromRevokedQueueGroups()
    {
        // Arrange
        var client = new Mock<IContactCenterHubClient>();
        var clients = new Mock<IHubClients<IContactCenterHubClient>>();
        var groupManager = new Mock<IGroupManager>();
        var shellSettings = new ShellSettings
        {
            Name = "TenantA",
        };
        var userGroup = TenantSignalRGroupName.ForUser(shellSettings.Name, "u1");
        var queueGroup = TenantSignalRGroupName.ForGroup(shellSettings.Name, ContactCenterHub.QueueGroup("q2"));

        clients.Setup(c => c.Group(userGroup)).Returns(client.Object);

        var hubContext = new Mock<IHubContext<ContactCenterHub, IContactCenterHubClient>>();
        hubContext.SetupGet(c => c.Clients).Returns(clients.Object);
        hubContext.SetupGet(c => c.Groups).Returns(groupManager.Object);

        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager.Setup(m => m.FindByUserIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSession
            {
                UserId = "u1",
                ConnectionIds = ["connection-1", "connection-2"],
            });

        var notifier = new ContactCenterRealTimeNotifier(
            hubContext.Object,
            sessionManager.Object,
            shellSettings);

        // Act
        await notifier.NotifyAgentMembershipChangedAsync(
            "u1",
            ["q2"],
            TestContext.Current.CancellationToken);

        // Assert
        groupManager.Verify(
            manager => manager.RemoveFromGroupAsync(
                "connection-1",
                queueGroup,
                It.IsAny<CancellationToken>()),
            Times.Once);
        groupManager.Verify(
            manager => manager.RemoveFromGroupAsync(
                "connection-2",
                queueGroup,
                It.IsAny<CancellationToken>()),
            Times.Once);
        client.Verify(c => c.MembershipChanged(), Times.Once);
    }
}
