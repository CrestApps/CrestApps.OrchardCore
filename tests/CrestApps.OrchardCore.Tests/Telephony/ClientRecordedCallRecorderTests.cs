using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// What the platform records of a call the soft phone placed itself, from the phone's own reports: that it is still
/// up, and that it ended.
/// </summary>
public sealed class ClientRecordedCallRecorderTests
{
    private static readonly DateTime _now = new(2026, 9, 25, 0, 16, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MarkAliveAsync_StampsEachReportedCall_AndWhenItFirstConnected()
    {
        // Arrange
        var ringing = CreateCall("interaction-1", "browser-1");
        var talking = CreateCall("interaction-2", "browser-2");
        var store = CreateStore(ringing, talking);
        var recorder = CreateRecorder(store);

        // Act
        var marked = await recorder.MarkAliveAsync("user-1", ["browser-1", "browser-2"], ["browser-2"], TestContext.Current.CancellationToken);
        var later = CreateRecorder(store, _now.AddSeconds(30));
        await later.MarkAliveAsync("user-1", ["browser-2"], ["browser-2"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, marked);
        Assert.Equal(_now, ActivityOf(ringing).LastReportedUtc);
        Assert.Null(ActivityOf(ringing).ConnectedUtc);
        Assert.Equal(_now.AddSeconds(30), ActivityOf(talking).LastReportedUtc);
        Assert.Equal(_now, ActivityOf(talking).ConnectedUtc);
    }

    // A report can only speak for a call the phone placed and that is still in progress: a call a provider tracks, or
    // one already settled, is not the phone's to keep alive.
    [Fact]
    public async Task MarkAliveAsync_LeavesProviderTrackedAndSettledCallsAlone()
    {
        // Arrange
        var providerCall = CreateCall("interaction-1", "call-1");
        providerCall.ProviderName = "Telnyx";
        var settled = CreateCall("interaction-2", "browser-2");
        settled.Outcome = CallOutcome.Completed;
        settled.EndedUtc = _now.AddMinutes(-1);
        var store = CreateStore(providerCall, settled);
        var recorder = CreateRecorder(store);

        // Act
        var marked = await recorder.MarkAliveAsync("user-1", ["call-1", "browser-2", "browser-unknown"], [], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, marked);
        Assert.False(providerCall.Has<ClientRecordedCallActivity>());
        Assert.False(settled.Has<ClientRecordedCallActivity>());
    }

    // The end the phone reports can say "never connected" when the page that placed the call went away and a new
    // one reported it; the phone's earlier reports said it did connect, and that is what the history keeps.
    [Fact]
    public async Task SettleAsync_KeepsACallTheReportsSawConnected_Completed()
    {
        // Arrange
        var call = CreateCall("interaction-1", "browser-1");
        call.Put(new ClientRecordedCallActivity { LastReportedUtc = _now.AddSeconds(-20), ConnectedUtc = _now.AddMinutes(-2) });
        var store = CreateStore(call);
        var recorder = CreateRecorder(store);

        // Act
        var settled = await recorder.SettleAsync("user-1", "browser-1", connected: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(settled);
        Assert.Equal(CallOutcome.Completed, call.Outcome);
        Assert.Equal(_now, call.EndedUtc);
        Assert.Equal(180, call.DurationSeconds);
        Assert.False(ActivityOf(call).EndedUnreported);
    }

    [Fact]
    public async Task SettleAsync_CancelsACallThatNeverConnected()
    {
        // Arrange
        var call = CreateCall("interaction-1", "browser-1");
        var store = CreateStore(call);
        var recorder = CreateRecorder(store);

        // Act
        await recorder.SettleAsync("user-1", "browser-1", connected: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallOutcome.Canceled, call.Outcome);
    }

    // A second report of the same end -- the phone resends an end it could not confirm -- changes nothing.
    [Fact]
    public async Task SettleAsync_NeverOverwritesACallAlreadySettled()
    {
        // Arrange
        var call = CreateCall("interaction-1", "browser-1");
        call.Outcome = CallOutcome.Completed;
        call.EndedUtc = _now.AddMinutes(-1);
        call.DurationSeconds = 60;
        var store = CreateStore(call);
        var recorder = CreateRecorder(store);

        // Act
        var settled = await recorder.SettleAsync("user-1", "browser-1", connected: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(settled);
        Assert.Equal(_now.AddMinutes(-1), call.EndedUtc);
        Assert.Equal(60, call.DurationSeconds);
    }

    private static ClientRecordedCallRecorder CreateRecorder(Mock<ITelephonyInteractionStore> store, DateTime? now = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(now ?? _now);

        return new ClientRecordedCallRecorder(store.Object, clock.Object);
    }

    private static Mock<ITelephonyInteractionStore> CreateStore(params TelephonyInteraction[] interactions)
    {
        var store = new Mock<ITelephonyInteractionStore>();

        foreach (var interaction in interactions)
        {
            store
                .Setup(value => value.FindByCallIdAsync(interaction.UserId, interaction.CallId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);
        }

        store.SetupRetryingUpdates(interactions);

        return store;
    }

    private static ClientRecordedCallActivity ActivityOf(TelephonyInteraction interaction)
        => interaction.TryGet<ClientRecordedCallActivity>(out var activity) ? activity : null;

    private static TelephonyInteraction CreateCall(string interactionId, string callId)
        => new()
        {
            InteractionId = interactionId,
            CallId = callId,
            UserId = "user-1",
            Direction = CallDirection.Outbound,
            To = "+17024993350",
            Outcome = CallOutcome.InProgress,
            StartedUtc = _now.AddMinutes(-3),
        };
}
