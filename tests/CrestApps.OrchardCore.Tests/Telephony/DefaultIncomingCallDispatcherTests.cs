using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DefaultIncomingCallDispatcherTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 9, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DispatchAsync_CreatesInboundInteraction_AndPushesIncomingCall()
    {
        // Arrange
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var client = new Mock<ITelephonyClient>();
        var store = new Mock<ITelephonyInteractionStore>();
        var clock = new Mock<IClock>();
        var logger = new Mock<ILogger<DefaultIncomingCallDispatcher>>();
        var shellSettings = new ShellSettings
        {
            Name = "TenantA",
        };

        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(shellSettings.Name, "user-1"))).Returns(client.Object);
        clock.SetupGet(value => value.UtcNow).Returns(_now.UtcDateTime);
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelephonyInteraction)null);

        TelephonyInteraction createdInteraction = null;
        store.Setup(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()))
            .Callback<TelephonyInteraction, CancellationToken>((interaction, _) => createdInteraction = interaction)
            .Returns(() => Task.CompletedTask);

        var dispatcher = new DefaultIncomingCallDispatcher(
            hubContext.Object,
            [],
            store.Object,
            clock.Object,
            logger.Object,
            shellSettings);
        var call = new TelephonyCall
        {
            CallId = "call-1",
            ProviderName = "Asterisk",
            From = "+15550001000",
            To = "+15550002000",
            Direction = CallDirection.Inbound,
            StartedUtc = _now,
        };

        // Act
        await dispatcher.DispatchAsync("user-1", call, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(createdInteraction);
        Assert.Equal("call-1", createdInteraction.CallId);
        Assert.Equal("+15550001000", createdInteraction.From);
        Assert.Equal(CallDirection.Inbound, createdInteraction.Direction);
        Assert.Equal(CallOutcome.InProgress, createdInteraction.Outcome);

        client.Verify(
            value => value.IncomingCall(
                It.Is<TelephonyCall>(incoming => incoming.CallId == "call-1"),
                It.IsAny<IncomingCallContext>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_RefreshesExistingInboundInteraction()
    {
        // Arrange
        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        var clients = new Mock<IHubClients<ITelephonyClient>>();
        var client = new Mock<ITelephonyClient>();
        var store = new Mock<ITelephonyInteractionStore>();
        var clock = new Mock<IClock>();
        var logger = new Mock<ILogger<DefaultIncomingCallDispatcher>>();
        var shellSettings = new ShellSettings
        {
            Name = "TenantA",
        };
        var existing = new TelephonyInteraction
        {
            InteractionId = "int-1",
            CallId = "call-1",
            From = "+15551110000",
            To = "+15552220000",
            Direction = CallDirection.Outbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _now.UtcDateTime.AddMinutes(-1),
        };

        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser(shellSettings.Name, "user-1"))).Returns(client.Object);
        clock.SetupGet(value => value.UtcNow).Returns(_now.UtcDateTime);
        store.Setup(value => value.FindByCallIdAsync("user-1", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var dispatcher = new DefaultIncomingCallDispatcher(
            hubContext.Object,
            [],
            store.Object,
            clock.Object,
            logger.Object,
            shellSettings);
        var call = new TelephonyCall
        {
            CallId = "call-1",
            ProviderName = "Asterisk",
            From = "+15550001000",
            To = "+15550002000",
            Direction = CallDirection.Inbound,
        };

        // Act
        await dispatcher.DispatchAsync("user-1", call, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+15550001000", existing.From);
        Assert.Equal("+15550002000", existing.To);
        Assert.Equal(CallDirection.Inbound, existing.Direction);

        store.Verify(value => value.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(value => value.CreateAsync(It.IsAny<TelephonyInteraction>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
