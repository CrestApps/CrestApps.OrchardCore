using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A call a colleague handed over rings this user on a leg the platform placed from its own number; the call history
/// keeps showing who is actually calling.
/// </summary>
public sealed class TelephonyTransferredCallHistoryTests
{
    private const string Caller = "+17025550101";
    private const string PlatformNumber = "+17785550000";

    [Fact]
    public async Task AnIncomingCallKeepsItsCaller_WhenItsLegReportsThePlatformsOwnNumber()
    {
        // Arrange
        var entry = Entry(CallDirection.Inbound, from: Caller);

        // Act
        var call = await ProjectAsync(entry, from: PlatformNumber);

        // Assert
        Assert.Equal(Caller, entry.From);
        Assert.Equal(Caller, call.From);
    }

    [Fact]
    public async Task AnIncomingCallWithNoCallerYet_TakesTheOneItsEventReports()
    {
        // Arrange
        var entry = Entry(CallDirection.Inbound, from: null);

        // Act
        await ProjectAsync(entry, from: Caller);

        // Assert
        Assert.Equal(Caller, entry.From);
    }

    [Fact]
    public async Task AnOutgoingCall_StillTakesTheCallerIdItsEventReports()
    {
        // Arrange
        var entry = Entry(CallDirection.Outbound, from: "+17785550999");

        // Act
        await ProjectAsync(entry, from: PlatformNumber);

        // Assert
        Assert.Equal(PlatformNumber, entry.From);
    }

    private static TelephonyInteraction Entry(CallDirection direction, string from)
        => new()
        {
            InteractionId = "interaction-1",
            CallId = "call-1",
            ProviderName = "provider-1",
            UserId = "user-1",
            From = from,
            Direction = direction,
            Outcome = CallOutcome.InProgress,
            StartedUtc = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc),
            AwaitingAnswer = direction == CallDirection.Inbound,
        };

    private static async Task<TelephonyCall> ProjectAsync(TelephonyInteraction entry, string from)
    {
        var store = new Mock<ITelephonyInteractionStore>();
        store.SetupRetryingUpdates(entry);

        TelephonyCall pushed = null;
        var client = new Mock<ITelephonyClient>();
        client.Setup(value => value.CallStateChanged(It.IsAny<TelephonyCall>()))
            .Callback<TelephonyCall>(call => pushed = call)
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients<ITelephonyClient>>();
        clients.Setup(value => value.Group(TenantSignalRGroupName.ForUser("TenantA", "user-1"))).Returns(client.Object);

        var hubContext = new Mock<IHubContext<TelephonyHub, ITelephonyClient>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 9, 25, 12, 0, 5, DateTimeKind.Utc));

        var handler = new TelephonyCallHistoryVoiceEventHandler(
            store.Object,
            hubContext.Object,
            clock.Object,
            NullLogger<TelephonyCallHistoryVoiceEventHandler>.Instance,
            new ShellSettings { Name = "TenantA" });

        await handler.HandleAsync(new ProviderVoiceEvent
        {
            ProviderName = "provider-1",
            ProviderCallId = "call-1",
            State = VoiceCallState.Connected,
            FromAddress = from,
            ToAddress = "sip:gencred2@sip.telnyx.com",
        }, TestContext.Current.CancellationToken);

        return pushed;
    }
}
