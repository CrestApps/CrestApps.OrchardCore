using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterOfferAnsweredNotifierTests
{
    private static readonly ShellSettings _shellSettings = new()
    {
        Name = "TenantA",
    };

    [Fact]
    public async Task NotifyAnsweredAsync_TellsEveryClientOnBothHubsAtOnce()
    {
        // Arrange
        // The durable OfferRevoked reached the agent's other pages a second after the accept; until then they kept
        // ringing in the headset for a call that had already been answered.
        var telephonyClient = new Mock<ITelephonyClient>();
        IncomingCallAnsweredNotification pushed = null;
        telephonyClient
            .Setup(client => client.IncomingCallAnswered(It.IsAny<IncomingCallAnsweredNotification>()))
            .Callback<IncomingCallAnsweredNotification>(notification => pushed = notification)
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, "u1"))).Returns(telephonyClient.Object);
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var realTimeNotifier = new Mock<IContactCenterRealTimeNotifier>();
        AgentOfferRevokedNotification revoked = null;
        realTimeNotifier
            .Setup(notifier => notifier.NotifyOfferRevokedAsync(It.IsAny<AgentOfferRevokedNotification>(), It.IsAny<CancellationToken>()))
            .Callback<AgentOfferRevokedNotification, CancellationToken>((notification, _) => revoked = notification)
            .Returns(Task.CompletedTask);

        var notifier = new ContactCenterOfferAnsweredNotifier(
            realTimeNotifier.Object,
            hubContext.Object,
            NullLogger<ContactCenterOfferAnsweredNotifier>.Instance,
            _shellSettings);

        var reservation = new ActivityReservation { ItemId = "r1", AgentId = "a1", ActivityItemId = "act1", QueueId = "q1" };

        // Act
        await notifier.NotifyAnsweredAsync(reservation, "u1", "call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(pushed);
        Assert.Equal("call-1", pushed.CallId);
        Assert.Equal("r1", pushed.OfferId);
        Assert.NotNull(revoked);
        Assert.Equal("u1", revoked.UserId);
        Assert.Equal("r1", revoked.ReservationId);
        Assert.Equal(AgentOfferRevokedReason.Accepted, revoked.Reason);
    }

    [Fact]
    public async Task NotifyAnsweredAsync_WhenAHubFails_DoesNotFailTheAccept()
    {
        // Arrange
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients.Setup(value => value.Group(It.IsAny<string>())).Throws(new InvalidOperationException("The backplane is down."));
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var notifier = new ContactCenterOfferAnsweredNotifier(
            new Mock<IContactCenterRealTimeNotifier>().Object,
            hubContext.Object,
            NullLogger<ContactCenterOfferAnsweredNotifier>.Instance,
            _shellSettings);

        // Act
        var exception = await Record.ExceptionAsync(() => notifier.NotifyAnsweredAsync(
            new ActivityReservation { ItemId = "r1", AgentId = "a1" },
            "u1",
            "call-1",
            TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }
}
