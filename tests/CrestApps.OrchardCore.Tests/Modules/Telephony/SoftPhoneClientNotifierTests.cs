using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

public sealed class SoftPhoneClientNotifierTests
{
    private static readonly ShellSettings _shellSettings = new()
    {
        Name = "TenantA",
    };

    [Fact]
    public async Task CallStateChangedAsync_PushesTheCallToTheUsersTenantGroup()
    {
        // Arrange
        var call = new TelephonyCall { CallId = "call-1", State = CallState.Connected };
        var (notifier, client, clients) = CreateNotifier("user-1");

        // Act
        await notifier.CallStateChangedAsync("user-1", call);

        // Assert
        client.Verify(value => value.CallStateChanged(call), Times.Once);
        clients.Verify(value => value.Group(TenantSignalRGroupName.ForUser("TenantA", "user-1")), Times.Once);
    }

    [Fact]
    public async Task IncomingCallAnsweredAsync_PushesTheNotificationToTheUsersTenantGroup()
    {
        // Arrange
        var notification = new IncomingCallAnsweredNotification { CallId = "call-1", OfferId = "offer-1" };
        var (notifier, client, _) = CreateNotifier("user-1");

        // Act
        await notifier.IncomingCallAnsweredAsync("user-1", notification);

        // Assert
        client.Verify(value => value.IncomingCallAnswered(notification), Times.Once);
    }

    [Fact]
    public async Task DialRequestedAsync_PushesTheRequestToTheUsersTenantGroup()
    {
        // Arrange
        var request = new TelephonyDialRequest { Number = "+14155552671" };
        var (notifier, client, _) = CreateNotifier("user-1");

        // Act
        await notifier.DialRequestedAsync("user-1", request);

        // Assert
        client.Verify(value => value.DialRequested(request), Times.Once);
    }

    [Fact]
    public async Task DialRequestedAsync_NeverReachesAnotherUsersGroup()
    {
        // Arrange
        var (notifier, client, clients) = CreateNotifier("user-1");

        // Act
        await notifier.DialRequestedAsync("user-2", new TelephonyDialRequest { Number = "+14155552671" });

        // Assert
        clients.Verify(value => value.Group(TenantSignalRGroupName.ForUser("TenantA", "user-2")), Times.Once);
        client.Verify(value => value.DialRequested(It.IsAny<TelephonyDialRequest>()), Times.Never);
    }

    private static (SoftPhoneClientNotifier Notifier, Mock<ITelephonyClient> Client, Mock<IHubClients<ITelephonyClient>> Clients) CreateNotifier(string userId)
    {
        var client = new Mock<ITelephonyClient>();
        client.Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>())).Returns(Task.CompletedTask);
        client.Setup(value => value.IncomingCallAnswered(It.IsAny<IncomingCallAnsweredNotification>())).Returns(Task.CompletedTask);
        client.Setup(value => value.DialRequested(It.IsAny<TelephonyDialRequest>())).Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(_shellSettings.Name, userId))).Returns(client.Object);
        clients.Setup(value => value.Group(It.Is<string>(group => group != TenantSignalRGroupName.ForUser(_shellSettings.Name, userId)))).Returns(new Mock<ITelephonyClient>().Object);

        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        return (new SoftPhoneClientNotifier(hubContext.Object, _shellSettings), client, clients);
    }
}
